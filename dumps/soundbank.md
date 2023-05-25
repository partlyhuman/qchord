# Soundbank Notes

Binary contains

> GMS960800B  V1.0 October 1997 COPYRIGHT DREAM SA 1993-1997

This shows up in the datasheet for the Atmel SAM9713, an all-in-one MIDI control which is present in Q-Chord as Q615.

> Available firmware/sample set for turnkey designs
>
> — 8 Mbit GMS960800B (**)
> 
> — 8 Mbit CleanWave® GMS970800B

So this was basically an all-in-one solution from Atmel.

The note is amusing:

> (**) GMS960800B with express permission of Roland Corporation, special licensing conditions apply. WARNING: GMS960800B may not be installed in any musical instrument except for electronic keyboards and synthesizers that have a sale price of less than $75 FOB. Using this product in the manufacture of musical instruments or selling this product for use in a musical instrument (other than the exceptions noted above) is a violation of the intellectual property rights of Roland Corporation and will result in liability for infringement.

Seems like 1. this design isn't very custom, Suzuki had a kind of drop-in solution and 2. they are in violation of Roland's licensing terms since it's apparently designed SPECIFICALLY for cheap shitty toys!

Also found in the binary is the ASCII `GSSBK080` which is the name of the source sound font perhaps?


## SAM9713

[Datasheet here](https://www.dosdays.co.uk/media/dream/SAM9713.PDF).

One interesting possibility is that it seems to support digital audio.


## About the manufacturers

Dream, Makers of CleanWave sound banks and probably distributor of SAM713:

https://dream.fr/other-documents.html

https://dosdays.co.uk/topics/wavetable_audio.php

> Terratec files sometimes use a .TTS file extension - these files simply add a header to a .94B file, so could be manipulated to work with the SAM9407.


Software:

https://www.fmjsoft.com/awavestudio.html#main

http://www.studio4all.de/htmle/welcomeewst.html



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

Complete swap of upper & lower 512kb

```
❯ dd bs=1k skip=512 count=512 if=QC1-Q616-soundbank.bin of=a.bin
❯ dd bs=1k skip=0 count=512 if=QC1-Q616-soundbank.bin of=b.bin
❯ cat a.bin b.bin > q616-swap.bin

```

## PCM data

Import as 16-bit signed PCM 22khz mono

Are there markers between samples? Let's look at the end of the font since the samples go long to short and there should be more markers.

90% sure there's no markers but they're indexed elsewhere. Since PCM data has no reserved range it wouldn't really be possible.

So we should be pretty good to replace a PCM section in audio software imperfectly

First 134,356 bytes (give or take) sounds like garbage when played so could be data?



## PC Sound cards with SAM9713 chipset

[Guillemot Maxi Sound 64 Home Studio](https://retronn.de/imports/hs64_config_guide.html) comes with some DOS software?

https://retronn.de/imports/hwgal/hw_maxi_sound_64_home_studio.html

[EW64](https://www.vogonswiki.com/index.php/EWS64)

https://retronn.de/imports/hwgal/hw_ews64xl_front.html


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
