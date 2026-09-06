#!/usr/bin/env python3
import re
import os

def check_method_for_await(content, method_start):
    """Extrai método a partir da linha de assinatura e verifica se tem await"""
    open_brace = content.find('{', method_start)
    if open_brace == -1:
        return None
    
    brace_count = 1
    pos = open_brace + 1
    while pos < len(content) and brace_count > 0:
        if content[pos] == '{':
            brace_count += 1
        elif content[pos] == '}':
            brace_count -= 1
        pos += 1
    
    if brace_count != 0:
        return None
    
    method_body = content[open_brace:pos]
    return 'await' in method_body

async_pattern = r'(private|protected|public|internal)?\s+async\s+(void|Task|Task<[^>]+>)\s+(\w+)\s*\('

files = [f for f in os.listdir('.') if f.endswith('.cs')]

results = []
for filename in sorted(files):
    try:
        with open(filename, 'r', encoding='utf-8') as f:
            content = f.read()
    except:
        with open(filename, 'r', encoding='latin-1') as f:
            content = f.read()
    
    for match in re.finditer(async_pattern, content):
        method_name = match.group(3)
        method_start = match.start()
        
        line_num = content[:method_start].count('\n') + 1
        
        has_await = check_method_for_await(content, method_start)
        
        if not has_await:
            results.append((filename, line_num, method_name))

for filename, line_num, method_name in results:
    print(f"{filename} | linha {line_num} | {method_name}")
