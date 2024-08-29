# Soundbank Notes

## Quick Ref

* Controller: SAM9713
* Soundbank ROM PART: GMS960800B
* Based on soundbank SET: GSSBK080

## Default instruments

| preset   | # | GM instrument name    |
----------------------------------------
| Guitar    | 26 | ELECTRIC GUITAR(JAZZ)    |
| Piano     | 0  | ACOUSTIC GRAND PIANO     |
| Strings   | 48 | STRINGS ENSEMBLE 1       |
| Vibes     | 11 | VIBRAPHONE               |
| Organ     | 19 | CHURCH ORGAN             |
| Voice     | 53 | VOICE OOOHS              |
| Flute     | 75 | PAN FLUTE                |
| Harp      | 46 | ORCHESTRAL HARP          |
| Synths    | 82 | LEAD 3(CALLIOPE)         |
| SFX       | 98 | Drum kit                 |

Appears as array in Program ROM @10A9h

```1A 00 30 0B 13 35 4B 2E 52 62```

## Creating a SAM9713 ROM from a 94B Soundbank

> This is now possible! See [video of the first successful test](https://www.youtube.com/watch?v=fGnvm3H4Y6E) where I created a ROM for SAM9713 injected with the GMBK9708 Dream/Roland sound set.

A ROM for SAM chips combine firmware for the target chip and a specially compiled and relocated 94B sound font. Using the SAM tools we can now make working ROMs for the SAM9713 / Q-Chord.

The 94B sound bank must fit in 1MB, and should be a complete General MIDI set (including instruments 0-99).

1. Obtain the `97PNP2_V50` cd image from https://archive.org/details/97-pnp-2-v-50
2. Mount the image and install the DREAM Editors from `SNDEDIT/SETUPED.EXE`. This can be done in a Windows 95-2000 VM, but should also still run in Windows 10!
3. Run the DREAM Bank Editor (`94WBANK.EXE`) and ignore the hardware not found error
4. Make a working directory like `C:\TEMP`
5. Use Sound Bank > New > Empty Sound Bank to create a new sound bank and save it to the working directory, like `C:\TEMP\TEMP.94K`
6. Copy your 94B sound bank and name it after your project e.g. `C:\TEMP\TEMP.94B`. As long as you don't compile the new soundbank, the tool assumes that this is the output of compiling your sound bank and you can perform these steps without having a source `94K`
7. Use Tran[s]fer > Binary File to start preparing a ROM
8. Use a Start Address of `0x3400` and a Memory Size of `0x80000` and hit OK. The addresses are in 16-bit words, so this refers to byte `0x6800` in the 1MB ROM. This relocates all the absolute addresses and sets things up to place the sound bank data in the same position as it was in the GMS960800B ROM.
9. Now, combine the firmware from 0h—6800h of the GMS960800B dump, and the sound bank generated in `C:\TEMP\TEMP.BIN`. You can use `dd`, or even more basic with `cat`:

```
cp GMS960800B.bin firmware.bin
truncate firmware.bin --size 26624
cat firmware.bin TEMP.BIN > newrom.bin
truncate newrom.bin --size 1048576
```

## Findings

Dump of ROM contains the ASCII text

> GMS960800B  V1.0 October 1997 COPYRIGHT DREAM SA 1993-1997

This shows up in the datasheet for the Atmel SAM9713, an all-in-one MIDI control which is present in Q-Chord as Q615.

> Available firmware/sample set for turnkey designs
>
> — 8 Mbit GS GMS960800B (**)
> 
> — 8 Mbit CleanWave® GMS970800B

So this was basically an all-in-one solution from Atmel.

The note is amusing:

> (**) GMS960800B with express permission of Roland Corporation, special licensing conditions apply. WARNING: GMS960800B may not be installed in any musical instrument except for electronic keyboards and synthesizers that have a sale price of less than $75 FOB. Using this product in the manufacture of musical instruments or selling this product for use in a musical instrument (other than the exceptions noted above) is a violation of the intellectual property rights of Roland Corporation and will result in liability for infringement.

Seems like 1. this design isn't very custom, Suzuki had a kind of drop-in solution and 2. they are in violation of Roland's licensing terms since it's apparently designed SPECIFICALLY for cheap shitty toys!

Also found in the binary is the ASCII `GSSBK080` which is the name of the source sound font used in the chipset (confirmed).


## SAM9713

[Datasheet](https://www.dosdays.co.uk/media/dream/SAM9713.PDF)

Docs for SAM9407, should be similar? *Note at least pinout differs, SAM9409 is TQFP144, SAM9713 is TQFP80*

[Programmer reference](sam9407-docs/SAM9407%20Programmer%20Reference.pdf) ⭐

[Datasheet](sam9407-docs/SAM9407_datasheet.pdf)

[Specs](sam9407-docs/SAM9407_specs.pdf)

Note UART MIDI mode, pretty sure this is connected to the QChord midi ports.

One interesting possibility is that it seems to support digital audio, unused by the Q-chord...

## About the manufacturers

Dream, Makers of CleanWave sound banks and probably distributor of SAM9713. I think Atmel purchased them at some point. Roland produced the sound banks and Dream stole, then licensed, them.

https://dream.fr/other-documents.html

https://dosdays.co.uk/topics/wavetable_audio.php

## PC Sound cards with SAM9713 chipset

The SAM9713 or its cousin the SAM9407 is found in at least two sound cards from the 1990s:

[Guillemot Maxi Sound 64 Home Studio](https://retronn.de/imports/hs64_config_guide.html) comes with some DOS software?

https://retronn.de/imports/hwgal/hw_maxi_sound_64_home_studio.html

[EW64](https://www.vogonswiki.com/index.php/EWS64)

https://retronn.de/imports/hwgal/hw_ews64xl_front.html

This has a bunch of 98b files

Great references:

* https://retronn.de/imports/hwgal/hwgal_empty.html and their FTP
* http://dosdays.co.uk/topics/wavetable_audio.php
* https://waveblaster.nl/

http://www.vogonsdrivers.com/getfile.php?fileid=144
> DMF format as used by Hoontech is identical to 94B, just rename file.
TTS format that is sometimes used by Terratec adds a header to the 94B file, but there is a TTS 94B converter available on the Terratec ftp.

> Dream released 4 new sound synthesis/processing ICs in March 1998. They were the SAM9707, SAM9703, SAM9713 and SAM9733. The SAM9707 was a marketed as a direct replacement for the SAM9407 on high-end PC sound cards and the SAM9703 was to replace the SAM9503 on high-end karoake systems. The SAM9713 was marketed for use in karaoke systems, and the SAM9733 for low-cost keyboards.

> The CleanWave 32(TM) is possibly a Roland patchset used without permission. Roland took Dream to court over the use of their samples/patches and won. On 2nd October 1997, Dream and Crystal Semiconductor acknowledged Roland's copyright in its digital sound recordings. Roland then authorised Dream (and its parent company, Atmel) to resell ICs with Roland GS recordings on. CleanWave 32 came with 128 GM instruments plus 195 variations, 9 drumsets and 1 sound effects set.

> Dream also produced a CleanWave 8 (1 MB ROM), CleanWave 16 (2 MB ROM), and CleanWave 64 (8 MB ROM).

## Software

The widest array of software seems to be that bundled with the drivers for the sound cards mentioned above. The TerraTec FTP site has outstanding archives of these, though they need to be run in DOS and/or Windows 98/2000: ftp://retronn.de/driver/TerraTec/EWS/ ftp://retronn.de/driver/Guillemot/MaxiSound64HomeStudio/

Others:

https://www.fmjsoft.com/awavestudio.html#main

http://www.studio4all.de/htmle/welcomeewst.html

http://web.archive.org/web/20080314220936/ftp://www.ews64.com/download/vsampler17b1.zip

Tools and even gerbers for reference design for SAM9407 sound card 
https://archive.org/download/97-pnp-2-v-50
https://archive.org/details/manualzilla-id-5727655
found at
https://www.vogons.org/viewtopic.php?t=92271

## CleanWave8

The other ROM is GMS970800, now dumped. Seems to correlate to GMBK9708.94B. `GMBK9708` appears at 0x10180.

Unfortunately, it is not a drop-in replacement, likely because as we now know, the dump contains firmware+soundbank, and the GMS970800 dumped was for use with SAM9503. The correct part for SAM9713 is probably GMS970800*B*.

In an case, now we can 


# Conversions

## DLS to 94B

These appear to be most closely related

[MinimalMIDIPlayer](https://github.com/SamusAranX/MinimalMIDIPlayer)  can use DLS sound fonts as can 
[Timidity++](https://en.wikipedia.org/wiki/TiMidity%2B%2B)

https://retronn.de/imports/dream_tricks.html

> Is it possible to easily convert soundfonts to 94B format?

> In a way yes. The Maxi Sound 64 Utilities allow to load DLS sound fonts directly. Internally the Utilities convert the DLS sound font on the fly to a 94B file. The program asks if you want to save the generated 94B elsewhere. It loads the 94B and deletes the temporary generated file. In combination with the previously described bug in the Win9x driver the 94B is already deleted when the Utilties ask to save it when the load fails. The only way is to copy the generated 94B manually from the TEMP folder while the error message is displayed. In NT4 however everything works fine.
There are tools to convert the common SBK, SF2 fonts to DLS first. It is unclear if specific information gets lost on conversion. It appears however that the resulting 94B sound fonts are good.
To convert SF2 to DLS the Extreme Sample Converter 3.6.0 Demo can be used. Choose as Source Format SF2 and as Destination Format DLS/Bank, press convert.


## SF2 to 94B

Documenting what happens in [this video](https://www.youtube.com/watch?v=MRmAzK0iHUY):

> I have not found a program to convert .sf2 files to .94b directly
> 
> I convert .sf2 files to .dls using Awave Studio
> 
> Then, use dlsread.exe to convert .dls to .wav, .94i, .94l
> 
> Finally: Guillemot Soundbank Manager, import .94l list to .94k and compile to .94b
> 
> Converting .sf2 to .dls in Awave Studio
> 
> 1. Delete unused & duplicate waveforms
> 2. Replace special characters like `(.;~"` from wave names
> 3. Same for instrument names
> 4. Save as DLS level 1 spec
> 5. Sometimes, do not save all regions (message)
> 
> How to convert DLS to Sound Bank (.94b)
> 
> 1. Run dlsread.exe
> 2. File > Open, choose DLS file
> 3. Press "Extract Bank" and you can get .wav, .94i, .94l
> 4. Run 94ubank(?)
> 5. Sound Banks > New
> 6. Choose .94l from #3
> 7. Press OK, .94k will be made
> 8. Sound Banks > Open, choose .94k
> 9. Press COMPILE, .94b will be made
> 
> DLS site: http://www.microsoft.com/music/setup.htm
> 

## 94B to Anything

https://www.vogons.org/viewtopic.php?t=58271

https://www.vogons.org/viewtopic.php?f=62&t=56535

## Using Flash in its place

Verified copying to a MX29F800 works, with a small tweak:

Ensure pins 43/WE# and 44/RESET# are pulled HIGH. These are NC in the PCB so jumper them to 33/BYTE.

## Roadmap to generate Omnichord ROM

1. Extract firmware and master assembly of 94b + firmware into ROMs
2. Use impulse tracker instruments from https://github.com/msx2plus/msx_iti_collection
3. Assemble in Awave Studio
4. Export DLS level 1
5. Use either Ed!son (EMS64) or Maxi Utilities (works without card?) or DREAM Bank Editor (97PNP) to convert to 94B

OR,
3. Extract samples with Awave or other
4. Go straight into DREAM Bank Editor & DREAM Instrument editor and create the instruments from scratch
5. Make a 94B directly
