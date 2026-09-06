#!/usr/bin/env python3
import re
import os

def get_method_body(content, start_pos):
    """Extrai o corpo completo de um método a partir de sua assinatura"""
    # Encontra o primeiro {
    brace_start = content.find('{', start_pos)
    if brace_start == -1:
        return None
    
    # Encontra o } correspondente
    count = 0
    for i in range(brace_start, len(content)):
        if content[i] == '{':
            count += 1
        elif content[i] == '}':
            count -= 1
            if count == 0:
                return content[brace_start:i+1]
    return None

async_method_sig = r'(?:private|protected|public|internal)?\s+async\s+(?:void|Task|Task<[^>]+>)\s+(\w+)\s*\('

files = [f for f in os.listdir('.') if f.endswith('.cs')]

for filename in sorted(files):
    try:
        with open(filename, 'r', encoding='utf-8') as f:
            content = f.read()
    except:
        with open(filename, 'r', encoding='latin-1') as f:
            content = f.read()
    
    for match in re.finditer(async_method_sig, content):
        method_name = match.group(1)
        method_start = match.start()
        
        body = get_method_body(content, method_start)
        if body is None:
            continue
        
        # Verifica se há 'await' no corpo (case-insensitive para estar seguro)
        if 'await' not in body.lower():
            line_num = content[:method_start].count('\n') + 1
            print(f"{filename:40} | linha {line_num:3d} | {method_name}")
