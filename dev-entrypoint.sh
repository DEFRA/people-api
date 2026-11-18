#!/bin/sh
# exec replaces this shell process so dotnet receives signals directly, not the shell.
exec dotnet watch --project ./defra-people-api run --urls "http://*:${PORT}"
