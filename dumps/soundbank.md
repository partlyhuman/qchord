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


## Possibilities

Without understanding the format, one thing you could do is swap in the other mentioned wavetable, GMS970800B. No idea if that would be findable or sound significantly different.


## Figuring out the GS file format


Makers of CleanWave sound banks:

https://dream.fr/other-documents.html

https://www.dream.fr/pdf/Serie5000/Soundbanks/GMBK5X64.pdf


https://openmidiproject.osdn.jp/documentations_en.html

The instrument list here matches up with the q-chord manual.

https://coolsoft.altervista.org/en/virtualmidisynth#soundfonts

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

## Compared to GSSBK080

Found and included GSSBK080.94B. The file is not identical but there are regions that are the same!

Addresses:

```
Dump			GSSBK080
0hD6272		0h65658	

```