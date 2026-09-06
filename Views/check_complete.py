#!/usr/bin/env python3
"""
Verifica cada método async e vê se tem await em qualquer lugar dentro dele
"""
import re
import os

def extract_method_lines(lines, start_line_idx):
    """Extrai as linhas do método começando no start_line_idx"""
    # Tenta encontrar em expressão  =>
    if '=>' in lines[start_line_idx]:
        return [lines[start_line_idx]]
    
    # Procura pelo corpo
    brace_count = 0
    found_brace = False
    result = []
    
    for i in range(start_line_idx, min(start_line_idx + 100, len(lines))):
        result.append(lines[i])
        
        for char in lines[i]:
            if char == '{':
                brace_count += 1
                found_brace = True
            elif char == '}':
                brace_count -= 1
                if found_brace and brace_count == 0:
                    return result
    
    return result

files = [f for f in os.listdir('.') if f.endswith('.cs')]

for filename in sorted(files):
    try:
        with open(filename, 'r', encoding='utf-8') as f:
            lines = f.readlines()
    except:
        with open(filename, 'r', encoding='latin-1') as f:
            lines = f.readlines()
    
    for i, line in enumerate(lines):
        match = re.search(r'async\s+(void|Task[^(]*)\s+(\w+)\s*\(', line)
        if match:
            method_name = match.group(2)
            
            # Extrai linhas do método
            method_lines = extract_method_lines(lines, i)
            method_text = ''.join(method_lines)
            
            # Verifica await
            if 'await' not in method_text:
                print(f"{filename:40} | linha {i+1:3d} | {method_name:30s} | NO AWAIT FOUND")
