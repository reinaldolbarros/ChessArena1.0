#!/bin/bash

for file in *.cs; do
    # Find all async methods
    while IFS= read -r line; do
        if [[ $line =~ (private|protected|public)\ (async)\ (void|Task|Task\<[^\>]+\>)\ ([a-zA-Z_][a-zA-Z0-9_]*) ]]; then
            method_name="${BASH_REMATCH[4]}"
            line_num=$(grep -n "$line" "$file" | head -1 | cut -d: -f1)
            
            # Get method body until closing brace
            start_line=$((line_num))
            # Extract method body
            method_body=$(sed -n "${start_line},/^[[:space:]]*}[[:space:]]*$/p" "$file" | tail -n +2)
            
            # Check if body contains 'await'
            if ! echo "$method_body" | grep -q "await"; then
                echo "FILE: $file | LINE: $line_num | METHOD: $method_name"
            fi
        fi
    done < <(grep -n "async.*void\|async.*Task" "$file")
done
