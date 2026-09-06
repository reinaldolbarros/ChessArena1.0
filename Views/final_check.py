#!/usr/bin/env python3
import re
import os

def get_method_full(content, match_start):
    """Pega a assinatura completa e corpo do método"""
    # Tenta encontrar até o final da linha de assinatura
    line_end = content.find('\n', match_start)
    if line_end == -1:
        line_end = len(content)
    
    first_line = content[match_start:line_end]
    
    # Se tem => é expression body
    if '=>' in first_line:
        return first_line
    
    # Senão, procura pelo corpo
    brace_start = content.find('{', match_start)
    if brace_start == -1:
        return first_line
    
    count = 0
    for i in range(brace_start, len(content)):
        if content[i] == '{':
            count += 1
        elif content[i] == '}':
            count -= 1
            if count == 0:
                return content[match_start:i+1]
    
    return content[match_start:min(match_start+500, len(content))]

async_pattern = r'(?:private|protected|public|internal)?\s+async\s+(?:void|Task|Task<[^>]+>)\s+(\w+)\s*\('

files = [f for f in os.listdir('.') if f.endswith('.cs')]

for filename in sorted(files):
    try:
        with open(filename, 'r', encoding='utf-8') as f:
            content = f.read()
    except:
        with open(filename, 'r', encoding='latin-1') as f:
            content = f.read()
    
    for match in re.finditer(async_pattern, content):
        method_name = match.group(1)
        method_start = match.start()
        
        full_method = get_method_full(content, method_start)
        
        # Simples: se não tem 'await' em lugar nenhum
        if 'await' not in full_method:
            line_num = content[:method_start].count('\n') + 1
            print(f"{filename:40} | linha {line_num:3d} | {method_name}")
