Nmm2Bcr - NMM Data to BCR Converter
===================================

A standalone command line tool that converts files produced by the [SIOS](https://sios-de.com) NMM-1.
The produced GPS data files are formatted according to ISO 25178-7, ISO 25178-71 and EUNA 15178 (BCR). All files are ASCII (text) files, the (currently deprecated) option to produce binary files is not implemented. 

### Command Line Usage:  

```
Nmm2Bcr inputfile [outputfile] [options]
```

### Options:  

`--channel (-c)` : The channel to be used as topography data. The default is "-LZ+AZ" (the height)
`--scan (-s)` : Scan index for multi-scan files.
`--profile (-p)` : Extract a single profile. If `--profile=0` extract whole scan field. 
`--both` : Use the average of forward and backtrace scan data (when present).
`--back` : Use the backtrace scan data only (when present).
`--diff` : Use the difference of forward and backtrace scan data (when present).
`--iso` : Force output file to be ISO 25178-71:2012 compliant.
`--quiet (-q)` : Quiet mode. No screen output (except for errors).
`--relaxed` : Allow large (>65535) field dimension. This is a violation of the format definition standards so the produced files might be unreadable by most evaluation software.
`--comment` : User supplied string to be included in the metadata.
`--bias (-b)` : Bias value in µm to be subtracted from the hight values (for `-r5` only).
`--reference (-r)` : Kind of height reference technique, supported values are:
   1 reference to minimum hight value
   2 reference to maximum hight value
   3 reference to average hight value
   4 reference to central hight value (average of minimum and maximum)
   5 reference to user supplied bias value
   6 reference to first value of scan field
   7 reference to last value of scan field
   8 reference to the hight value of the center of scan field (or profile)
   9 reference to connecting plane (or line)
   10 reference to LSQ plane (or line)
   11 same as 9 but positive definite
   12 same as 10 but positive definite

### Caveats and technical details:  
some text

### Dependencies  
Bev.IO.NmmReader:  https://github.com/matusm/Bev.IO.NmmReader  
Bev.IO.BcrWriter: https://github.com/matusm/Bev.IO.BcrWriter 
CommandLineParser: https://github.com/commandlineparser/commandline 

The MIT License (MIT)
Copyright (c) 2019-2020 Michael Matus