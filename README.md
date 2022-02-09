Nmm2Bcr - NMM Data to BCR Converter
===================================

A standalone command line tool that converts files produced by the [SIOS](https://sios-de.com) NMM-1.
The converted GPS data files are formatted according to ISO 25178-7, ISO 25178-71 and EUNA 15178 (BCR). All files are ASCII (text) files, the (currently deprecated) option to produce binary files is not implemented. 

## Command Line Usage:  

```
Nmm2Bcr inputfile [outputfile] [options]
```

## Options:  

`--channel (-c)` : The channel to be used as topography data. The default is "-LZ+AZ" (the height)

`--zscale (-z)` : Scale factor for height axis. Default is 1e-6 (µm).

`--scan (-s)` : Scan index for multi-scan files.

`--profile (-p)` : Extract a single profile. If `--profile=0` extract the whole scan field. 

`--both` : Use the average of forward and backtrace scan data (when present).

`--back` : Use the backtrace scan data only (when present).

`--diff` : Use the difference of forward and backtrace scan data (when present).

`--quiet (-q)` : Quiet mode. No screen output (except for errors).

`--comment` : User supplied string to be included in the metadata.

`--strict` : Disable large (>65535) field dimension and other goodies.

`--iso` : Force output file to be ISO 25178-71:2012 compliant (not recommended, Gwyddion will currently ignore metadata).

### Options for height data transformation

`--heydemann` : perform Heydemann correction, but only for the "-LZ+AZ" channel.

`--bias (-b)` : Bias value in µm to be subtracted from the hight values (for `-r5` only).

`--reference (-r)` : The height reference technique, supported values are:

0: do nothing

1: reference to minimum hight value (all values positive, default)

2: reference to maximum hight value (all values negative or 0)

3: reference to average hight value

4: reference to central hight value (average of minimum and maximum)

5: reference to user supplied bias value

6: reference to first value of scan field

7: reference to last value of scan field

8: reference to the hight value of the center of scan field (or profile)

9: reference to connecting plane (or line)

10: reference to LSQ plane (or line)

11: first apply 9, then 1

12: first apply 10, then 1

## Effect of the `--strict` command line option

* Field dimensions are restricted to 65535 at most;

* The ManufacID is trimmed to 10 characters;

* Invalid data points are coded by the string `BAD` instead of `NaN`.

## Dependencies  
Bev.IO.NmmReader:  https://github.com/matusm/Bev.IO.NmmReader  

Bev.IO.BcrWriter: https://github.com/matusm/Bev.IO.BcrWriter 

CommandLineParser: https://github.com/commandlineparser/commandline 
