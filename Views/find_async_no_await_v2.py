#!/usr/bin/env python3
import re
import os

async_pattern = r'(private|protected|public|internal)?\s+async\s+(void|Task|Task<[^>]+>)\s+(\w+)\s*\([^)]*\)\s*(?:=>|$)'

files = [f for f in os.listdir('.') if f.endswith('.cs')]

results = []
for filename in sorted(files):
    try:
        with open(filename, 'r', encoding='utf-8') as f:
            lines = f.readlines()
    except:
        with open(filename, 'r', encoding='latin-1') as f:
            lines = f.readlines()
    
    for i, line in enumerate(lines):
        match = re.search(r'async\s+(void|Task|Task<[^>]+>)\s+(\w+)\s*\(', line)
        if match:
            method_name = match.group(2)
            line_num = i + 1
            
            # Procurar pelo corpo do método até encontrar }
            # Se é expression body (=>), verificar apenas aquela linha
            if '=>' in line:
                if 'await' not in line:
                    results.append((filename, line_num, method_name))
            else:
                # Procurar até a chave de fechamento
                found_await = False
                for j in range(i, min(i + 100, len(lines))):
                    if 'await' in lines[j]:
                        found_await = True
                        break
                    if j > i and lines[j].strip().startswith('}'):
                        # Fim do método, não encontrou await
                        break
                
                if not found_await:
                    results.append((filename, line_num, method_name))

for filename, line_num, method_name in results:
    print(f"{filename} | linha {line_num} | {method_name}")
