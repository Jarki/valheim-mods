# Recorded firearm audio

Research and processing date: 2026-10-27. These are recordings of physical
firearms/explosions, not generated gunshot synthesis or ripped game assets.
Descriptions below distinguish the actual recorded equipment from its mod role.
Keep this document with any distribution containing these audio files.

## Credits and license evidence

### Pistol and AK shots — The Free Firearm Sound Library

- Creators/recordists: **Ben Jaszczak, Brian Nelson, Kevin Heras, and Matthew Nanney**.
- License: **CC0 1.0**, <https://creativecommons.org/publicdomain/zero/1.0/>.
- Collection and license evidence:
  <https://opengameart.org/content/the-free-firearm-sound-library>.
- Original project's archived statement, independently read:
  <https://web.archive.org/web/20141217041948/http://freefirearmsfx.com/>.
  Exact relevant text: “Our team holds CC0 NO RIGHTS RESERVED for this library.
  It may be used without royalty or credit ... for any application, personal or
  professional. Have at it!” OpenGameArt additionally states: “These sounds were
  created and recorded by Ben Jaszczak, Brian Nelson, Kevin Heras, and Matthew Nanney”.
- Download: <https://opengameart.org/sites/default/files/Prepared%20SFX%20Library.7z>.
- The original library guide distinguishes raw recordings from prepared effects:
  <https://web.archive.org/web/20141215065222/http://freefirearmsfx.com/the-library/4582574710>.
  It explicitly says the prepared recordings use **limiting and EQ**. These are
  processed real recordings, not untouched field recordings. No additional
  limiting/EQ was applied in this project.
- Pistol source: `Prepared SFX Library/1911/A_42P.wav`. Embedded original WAV
  comment: “1911, handgun, pistol, semi automatic, .45, gunshot, near distance,
  front of shooter, left, right, stereo”. `pistol_shot.wav` uses its first shot.
  **This is a real .45-caliber 1911 recording used as a pistol-report substitute;
  it is not a verified 9mm or Glock shot.** The matching 9mm Walther PPQ files are
  listed in the archive, but could not be obtained within the download timeouts.
- AK source: `Prepared SFX Library/AK-47/C_28P.wav`. Embedded original WAV comment:
  “AK-47, rifle, fully automatic, semi automatic, machine gun, assault rifle,
  7.62x39, gunshot, near distance, front of shooter, left, right, stereo”.
  This contains separate single shots; `ak_shot.wav` is intended to play once per
  fired round, including during automatic fire.

### Pistol reload — Sophia O. Caldwell

- Credit: **“Fast Pistol Reload [Bersa BP9CC 9x19]” by Sophia O. Caldwell (Sophia_C)**,
  <https://industrialgradenerd.com>.
- Source: <https://freesound.org/people/Sophia_C/sounds/467182/>.
- License: **CC BY 4.0**, <https://creativecommons.org/licenses/by/4.0/>.
  The source page's license link says “Attribution 4.0”. Its attribution field
  names Sophia O. Caldwell and the website above; credit is retained for this mod.
- Recording evidence: “A fast reload of a Bersa BP9CC pistol chambered in 9x19mm.
  I recorded it with a Blue Snowball desktop microphone and edited it in Audacity.”
  The author identifies magazine release followed by insertion, and explicitly
  says the magazines were empty. This is a real 9mm pistol, not a Glock recording,
  and contains magazine handling rather than a separate slide-release operation.
- Downloaded public high-quality preview:
  <https://cdn.freesound.org/previews/467/467182_6918403-hq.mp3>.
- Local adaptation: `pistol_reload.wav`; mono conversion, peak normalization,
  short edge fades and PCM conversion. No change to action timing.

### AK reload/handling — Mittelmorder

- Credit: **“AKManipulation.wav” by Mittelmorder**.
- Source: <https://freesound.org/people/Mittelmorder/sounds/122690/>.
- License: **CC BY 3.0**, <https://creativecommons.org/licenses/by/3.0/>.
  The source page's license link says “Attribution 3.0”.
- Recording evidence: “Sounds from a video I took of me shooting my AK47 style
  rifle, sound file includes a gunshot and reloading/manipulation sound effects.”
- Downloaded public high-quality preview:
  <https://cdn.freesound.org/previews/122/122690_1043663-hq.mp3>.
- Local adaptation: `ak_reload.wav`; excerpt of mechanical handling from
  3.05–5.95 seconds, mono conversion, peak normalization, short edge fades, PCM.
  The excerpt has multiple mechanical transients. The exact correspondence of
  each transient to magazine/bolt movements has not been verified by listening;
  treat animation synchronization as an outstanding integration check.
- The author also asks to see a link to the final product. No publication or
  contact was performed by this asset-preparation task.

### RPG launch — klangfabrik

- Credit: **“rpg launcher three shots.wav” by klangfabrik**.
- Source: <https://freesound.org/people/klangfabrik/sounds/249298/>.
- License: **CC0 1.0**, <https://creativecommons.org/publicdomain/zero/1.0/>.
  The source page's license link says “Creative Commons 0”.
- Recording evidence: “Three medium shot exterior firings of a rpg (rocket
  propelled grenade) launcher/anti-tank weapon.” The page does not establish a
  specific RPG model, microphone, or ammunition; do not label it verified RPG-7.
- Downloaded public high-quality preview:
  <https://cdn.freesound.org/previews/249/249298_181941-hq.mp3>.
- Local adaptation: `rpg_launch.wav`; first firing, 0.09–3.09 seconds, mono,
  peak normalization, short edge fades, PCM.

### Explosion — sidohzen, edited by qubodup

- Credit: **“Explosion, short [sidohzen 165808]” by qubodup**, from
  **“Explosion with debris - authentic. 4kg TNT” by sidohzen**.
- Edit/source: <https://freesound.org/people/qubodup/sounds/171971/>.
- Original recording: <https://freesound.org/people/sidohzen/sounds/165808/>.
- License: **CC0 1.0** on both independently checked pages,
  <https://creativecommons.org/publicdomain/zero/1.0/>.
- Recording evidence from sidohzen: “This is a demolition of land mines in
  Camdodia. ... Captured with an onboard mic on my Sony HDR-CX160.”
  This is an actual demolition explosion used as an RPG-impact sound; it is not
  represented as a recording of an RPG warhead. The source title's “4kg TNT”
  description is the author's statement, not independently measured.
- qubodup describes the edit as “Cut, fade in/out, normalize” in Audacity.
- Downloaded public high-quality preview:
  <https://cdn.freesound.org/previews/171/171971_71257-hq.mp3>.
- Local adaptation: `explosion.wav`; complete 1.5-second edit, mono conversion,
  peak normalization, short edge fades, PCM.

## Acquisition and verification notes

### Runtime adaptations

The plugin plays one shot recording per fired round. Reload playback is rescaled
to the gameplay duration (pistol: 1.8 seconds; AKM: 2.5 seconds). The RPG currently
uses `ak_reload.wav` as mechanical handling foley, rescaled to its 3-second reload;
this is not a recording of an actual RPG reload. Unity pitch-based rescaling also
changes pitch. These runtime changes do not modify the WAV hashes below.

### Processing recipe

All runtime files are **48,000 Hz, mono, signed PCM16 WAV**, with measured sample
peaks normalized to **−1 dBFS**. The recordings retain their original speed and
pitch. Stereo is averaged equally (`0.5*L + 0.5*R`), not summed at full volume.
FFmpeg decoding occurs in floating point before attenuation, avoiding clipping
of MP3 reconstruction peaks. No synthetic layers, reverb, or compression were
added. Playback volume still needs balancing in the mod.

The filter order is:

```text
atrim=start=START:duration=DURATION,asetpts=PTS-STARTPTS,
pan=mono|c0=0.5*c0+0.5*c1,aresample=48000,
afade=t=in:d=FADE_IN,afade=t=out:st=DURATION-FADE_OUT:d=FADE_OUT,
volume=GAIN
```

`GAIN = 10^(-1/20) / max(abs(samples))`, measured after all preceding filters.
Encode with `-map_metadata -1 -ar 48000 -ac 1 -c:a pcm_s16le`.

| Output | Source start (s) | Duration (s) | Fade in/out (s) | Applied gain (dB) |
| --- | ---: | ---: | --- | ---: |
| `pistol_shot.wav` | 0.930 | 1.100 | 0.001 / 0.050 | -1.029561 |
| `ak_shot.wav` | 0.600 | 1.000 | 0.001 / 0.050 | +1.660927 |
| `pistol_reload.wav` | 0 | 1.578021 | 0.005 / 0.020 | -3.632092 |
| `ak_reload.wav` | 3.050 | 2.900 | 0.005 / 0.020 | -0.928905 |
| `rpg_launch.wav` | 0.090 | 3.000 | 0.002 / 0.100 | -0.520991 |
| `explosion.wav` | 0 | 1.500 | 0.001 / 0.020 | -0.067430 |

The pistol reload trim requested 1.578 seconds; sample-rate rounding yields the
1.578021-second output shown above. Its fade-out starts at 1.558 seconds.

### Integrity

SHA-256 of downloaded/selected source bytes:

```text
8e84438e771c157155a6a1ff47a6a7a7d81b6f39b185d41e426c57337a82254a  A_42P.wav
e0934c1d79192d2216db62fdf6ab57bf9d5d585267af367a1cfb21f0972a537d  C_28P.wav
ea856dfe56048ff844966542eaab83f27cb2440ea7176a745ccf9aef45669848  467182_6918403-hq.mp3
65563bd654e656bd30851df8fa1b17fc4247d92d371ec7f173484a1d3701123e  122690_1043663-hq.mp3
ab11dd57d26ff89578644628ef3f587e594b89200ff440fe58075fd4b0c5c219  249298_181941-hq.mp3
e8c0bbfb811f37762bfdae906e7a64412f0a07b4cd19745842ab0646ab4d220c  171971_71257-hq.mp3
```

SHA-256 of runtime WAV files:

```text
1d03af9e8d1a29883a2917ff415968a1ad50ba3b542907c1c58b3482fb8610bd  pistol_shot.wav
99b608008efac5086cb707f0e2b63135a4728aaad62adb71e74e482885cca11c  ak_shot.wav
a37077ca1599a9b0581cd1245fe02019c4684a8f4fac5df6326a9d29d551100a  pistol_reload.wav
95079faf72096e077a21b2880fbb3cf2ae863678c56b2d886656863000e46a4d  ak_reload.wav
2fda855f602dd93422e0fb7b7c700525d2f6c66f20a37210f0563edf4a1495ad  rpg_launch.wav
7b2e3da2351593f6f93cb8205c765031a4794266c91c496dc3cb169b1ee6b071  explosion.wav
```

### Limitations

- The 194 MB solid 7z archive download was too slow and timed out. Its index
  was retrieved with an HTTP byte-range request; the complete early pistol/AK
  members were extracted from the downloaded prefix plus that index using
  `bsdtar --fast-read`. Both selected source members extracted successfully
  without errors. The entire archive was **not** downloaded or verified.
  Incomplete downloads and failed later-member extraction attempts remain only
  in the temporary workspace, never in this asset directory.
- Freesound originals require a login. These files use the openly served **HQ
  MP3 previews** explicitly linked in the public sound pages' sharing data.
  They are lossy recordings converted to PCM WAV; conversion does not recover
  the original WAV/FLAC quality. No account, cookies, credentials, or access
  control bypass was used.
- Original source claims and license statements were read, not inferred from
  search-result snippets. CC BY assets must retain author, title, source and
  license links above, plus these adaptation notices.
- Source/derived waveform checks are technical checks, not an audible listening
  review or an in-game verification. A listening/level check in the development
  profile is still needed, particularly AK reload movement timing.
- Temporary source archives, source previews and page HTML are under
  `/tmp/opencode/valheim-audio/`; they are not bundled as extra runtime clips.

## Research trail and exclusions

- Web-search integration returned cancellation errors; research continued with
  direct OpenGameArt and Freesound site search and source-page inspection.
- Freesound queries included `ak47`, `"rocket launcher"`, `"RPG-7"`,
  `"RPG" "firing"`, `"rocket" "launch" "government"`,
  `"explosion" "recorded"` (CC0 filter), `"reload" "recorded" "pistol"`,
  and `"AK" "magazine"`.
- Excluded synthesized examples and replica/airsoft sounds; e.g.
  <https://freesound.org/people/Physon712/sounds/580869/> explicitly records a
  replica. <https://freesound.org/people/Artninja/sounds/712553/> credits a
  third-party YouTube owner without evidence of that owner's license grant.
- <https://freesound.org/people/conkoni/sounds/562105/> documents a real launcher
  and on-set car explosion, but is CC BY-NC 4.0; omitted in favor of CC0.
- The original project's individual-download Google Drive folder now requires
  sign-in; used the explicitly licensed OpenGameArt archive instead.
- Project knowledge-base lookup `uv run um kb search "valheim"` timed out after
  120 seconds; no knowledge-base result was assumed.
