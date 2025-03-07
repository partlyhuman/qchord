#!/bin/zsh
if [[ -d bin ]]; then rm -rf bin/; fi

mkdir -p bin/win-x64 && cp */bin/Release/net8.0/win-x64/publish/*.exe bin/win-x64
mkdir -p bin/osx-x64 && cp */bin/Release/net8.0/osx-x64/publish/* bin/osx-x64
mkdir -p bin/osx-arm64 && cp */bin/Release/net8.0/osx-arm64/publish/* bin/osx-arm64

rm bin/*/*.pdb