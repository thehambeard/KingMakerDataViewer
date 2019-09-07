# Data Viewer for Pathfinder: Kingmaker
## Download
https://www.nexusmods.com/pathfinderkingmaker/mods/106
## Compile
This project depends on [ModMaker](https://github.com/hsinyuhcan/KingmakerModMaker), you need both repos in the same folder, and a folder called `KingmakerLib` including the Dll files. The folder structure should look like:
```
Repos
¢x
¢u¢w¢w KingmakerLib
¢x   ¢u¢w¢w UnityModManager
¢x   ¢x   ¢u¢w¢w 0Harmony12.dll
¢x   ¢x   ¢|¢w¢w UnityModManager.dll
¢x   ¢|¢w¢w *.dll
¢x
¢u¢w¢w KingmakerModMaker
¢x   ¢u¢w¢w ModMaker
¢x   ¢x   ¢|¢w¢w ModMaker.shproj
¢x   ¢|¢w¢w ModMaker.sln
¢x
¢|¢w¢w KingmakerDataViewer
    ¢u¢w¢w DataViewer
    ¢x   ¢|¢w¢w DataViewer.csproj
    ¢|¢w¢w DataViewer.sln
```
