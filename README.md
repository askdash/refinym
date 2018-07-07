RefiNym: Using Names to Refine Types
====

This is the repository for the code of the FSE 2018 paper [RefiNym: Using Names to Refine Types](TODO).
Please cite as 
```
@inproceedings{refinym2018dash,
   title={{RefiNym}: Using Names to Refine Types},
   authors={Santanu Kumar Dash and Miltiadis Allamanis and Earl T. Barr},
   booktitle={Foundations of Software Engineering (FSE)},
   year={2018}
}
```

Running the Tool
======
The code requires Windows and Visual Studio 2017 to compile.

To extract clusters in a given solution use the CLI:
```
./SubtypeMiningConsole.exe cluster <PathToSln> <repositoryPath> <githubLink> <OutputJsonFileName> <typeToCluster>
```

To rewrite a project with the refined types, given a clustering in `clusterings.json`, run the CLI with:
```
TODO
```
