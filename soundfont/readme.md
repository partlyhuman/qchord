# Soundbank Notes

## Quick Ref

* Controller: SAM9713
* Soundbank ROM PART: GMS960800B
* Based on soundbank SET: GSSBK080

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

Dream, Makers of CleanWave sound banks and probably distributor of SAM9713. I have found mention of Dream as a subsidiary of Atmel. Roland produced the sound banks.

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


## Compared to GSSBK080

Found and included GSSBK080.94B. The file is not identical but there are regions that are the same!

Looking at the audio waveforms it looks like it's ordered like:

```
94B: ABC DEF (audio data starts at 4.5s / 24s)
BIN: DEF ABC (audio data starts at 11.9s / 24s and loops from end to beginning)
```

Make them line up:

Appears that the sample data in the .94b starts with ascii "GH SAMP0"

Identical sections start at

```
GSSBK080					Dump			

FROM
0x1f402 (128002)		0x80020 (524320)

FOR
+ 0x7ffde

TO
0x9F3E0					0xffffe

---

FROM
0x9f402					0x07246

FOR
+ 0x61510

TO
0x100912				0x68756
```

```
0x1f402-0x9F3E0 of the sound font  === 0x80020-0xffffe of the ROM
0x9f402-0x100912 of the sound font === 0x07246-0x68756 of the ROM

❯ dd bs=1 skip=128002 count=524254 if=GSSBK080.94B of=A-GSSBK080.94b
❯ dd bs=1 skip=524320 count=524254 if=QC1-Q616-soundbank.bin of=A-Q616.bin

❯ dd bs=1 skip=29254 count=398608 if=QC1-Q616-soundbank.bin of=B-Q616.bin
❯ dd bs=1 skip=652290 count=398608 if=GSSBK080.94B of=B-GSSBK080.94b

```


## PCM data

Import as 16-bit signed PCM 22khz mono

Are there markers between samples? Let's look at the end of the font since the samples go long to short and there should be more markers.

90% sure there's no markers but they're indexed elsewhere. Since PCM data has no reserved range it wouldn't really be possible.

So we should be pretty good to replace a PCM section in audio software imperfectly

First 134,356 bytes (give or take) sounds like garbage when played so could be data?


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


## Verifying data arrangement

The straight copy to MX29F800 doesn't seem to work, so let's verify the pinout isn't unexpected, by tracing SAM94 to ROM socket.

I think we're looking for:

* WD0-15	PCM ROM data
* WA0-18	External memory address (ROM) *note this is by WORD so multiply addresses by 2, or WA0 is address bit 1*
* WCS0		PCM ROM Chip Select (active low)
* WOE		PCM ROM Output Enable (active low)
* GND
* VCC		+5V


Traced this all out on mainboard referencing pinout of SAM9713:

	NC		| 1 		44 | NC
	59 WA18	| 2 		43 | NC
	58 WA17	| 3 		42 | 47 WA8
	46 WA7	| 4 		41 | 48 WA9
	45 WA6	| 5 		40 | 49 WA10
	44 WA5	| 6 		39 | 50 WA11
	43 WA4	| 7 		38 | 51 WA12
	42 WA3	| 8 		37 | 52 WA13
	41 WA2	| 9 		36 | 53 WA14
	39 WA1	| 10		35 | 54 WA15
	37 WA0	| 11		34 | 55 WA16
	29 WCS0	| 12		33 | VCC (BYTE ALWAYS HIGH)
	GND		| 13		32 | GND
	31 WOE	| 14		31 | 4 WD15
	66 WD0	| 15		30 | 73 WD7
	75 WD8 	| 16		29 | 3 WD14
	67 WD1	| 17		28 | 72 WD6
	76 WD9	| 18		27 | 2 WD13
	68 WD2	| 19		26 | 71 WD5
	77 WD10	| 20		25 | 79 WD12
	69 WD3	| 21		24 | 70 WD4
	78 WD11	| 22		23 | 22/24 VCC 

Confirmed this matches MX23C8100
This matches MX29F800 as well (the SOP44 i'm using) but 44 is /RESET so this should be pulled high

## Using Flash in its place

Verified copying to a MX29F800 works, with a small tweak:

Ensure pins 43/WE# and 44/RESET# are pulled HIGH. These are NC in the PCB so jumper them to 33/BYTE.

## Experiment: Using a different 1MB GM .94B

From ftp://ftp.retronn.de/driver/TerraTec/EWS/64L/SoundSets has two alternative 1MB sound fonts:

* 1,021,666 94SBK080.94B
* 1,050,037 GMBK9708.94B
* 1,023,230 GSSBK080.94B

Let's try replacing the exact same byte ranges but use `GMBK9708.94B` instead:

```
dd if=font.94b bs=1 skip=128002 count=524254 seek=524320 of=rom.bin conv=notrunc
dd if=font.94b bs=1 skip=652290 count=398608 seek=29254 of=rom.bin conv=notrunc
```

Sounds WEIRD. There's got to be something to the nonmatching bytes, or there's more byte ranges to discover that are the same.
