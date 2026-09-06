#!/bin/bash

for file in BracketPage.xaml.cs CareerPage.xaml.cs DailyMissionsPage.xaml.cs FriendInvitePage.xaml.cs GamePage.xaml.cs LeaguePage.xaml.cs LobbyPage.xaml.cs LoginPage.xaml.cs SplashPage.xaml.cs TournamentLobbyPage.xaml.cs WaitingRoomPage.xaml.cs; do
    # Extrai as linhas com async
    grep -n "async.*void\|async.*Task" "$file" | while IFS=: read -r linenum rest; do
        # Pega a próxima linha e procura pelo fechamento do método
        found_await=0
        for ((i = linenum; i < linenum + 50; i++)); do
            line=$(sed -n "${i}p" "$file")
            if [[ "$line" =~ await ]]; then
                found_await=1
                break
            fi
            # Se chega no fechamento antes de encontrar await
            if [[ "$i" > "$linenum" && "$line" =~ ^[[:space:]]*} ]]; then
                break
            fi
        done
        
        if [[ $found_await -eq 0 ]]; then
            method=$(echo "$rest" | sed 's/.*async[[:space:]]*[^[:space:]]*[[:space:]]*\([^(]*\).*/\1/')
            echo "$file | linha $linenum | $method"
        fi
    done
done
