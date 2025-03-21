Get-ChildItem src -Include obj,bin,TestResults -Directory -Recurse | Remove-Item -Recurse -Force
Get-ChildItem -Include nupkg,versions -Directory -Recurse -Depth 0 | Remove-Item -Recurse -Force
Get-ChildItem src -Include .vs -Directory -Recurse -Hidden -Depth 0 | Remove-Item -Recurse -Force
Get-ChildItem -Filter *.binlog -Recurse -File | Remove-Item -Force