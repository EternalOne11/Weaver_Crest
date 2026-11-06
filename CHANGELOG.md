# Version History

## 1.0.0
Inital Public Release

## 1.0.1
Hotfix 
- Fixed binding timings... I forgot to fix this oops...
- They should now both properly line up timing wise, in the same way the vanilla ones do.
- Plus the tiny half frame display of the vanilla bind effect should be removed.

## 1.0.2
Hotfix 
- Fixes weird error that was causing some people's builds to not work correctly... 
Tied to either thunderstore.toml or ResourceUtils.cs. (probably system language issue)









## Playtests pre 1.0.0
### PT1
First playtest

### PT2
- Slowed down the attacks. (up and slash)
- Down attack cooldown reduced.
- The mask break now causes the screen to go to black
- The sister splinter vines and wood wasp nests no longer give silk.
- Removed glow from hitting enemies (was an oversight on my part)
- removed half silk when switching off the crest
- Half silk now merges with silk hearts (also bench regen, capping silk at 1 per silkheart like vanilla, instead of keeping the half)
- Muckmaggot degredation speed is now doubled for half silk notches. (attacking resets - it still, because silk hearts reset degredation effect) 
- Brought back the dash slash grunts...

### PT3
-  Mossberry+halfsilk merge interaction
- Fixed white flash spam at 9 silk, with no silk extensions)
- Bind cancel included (config option) (disabled by default)
- Mask break removed for lifeblood   (config option) (Allowed by default)
- 0.5 silk upon reaching 0 silk from a bind. (config option) (disabled by default...)
- 15% Silk skill damage reduction. (config option) (disabled by default...)
- Audio now scales with volume settings (I think) 
- Several Translations thanks to Ansbach and Mudkip2302

### PT4
- Updated bind to finish the bind, before it cancels.
- Half knockback (config option) (disabled by default)
- Barnicles now can be attacked properly
- Experimental Needolin Half silk (uses acid filter)
- hurt grunt sounds now follow voice toggle.
- The last mask effect now triggers when you trade your 2nd to last mask
- Removes a grunt sound during charge attacks. (was having issues)

### PT5
- Experimental build with silk refunds...
- (essentail ones are the only ones enabled by default)
- Known missing things are the white flowers in shellwood
- the red baloons in huntersmarch/marrow, and levers.
- breakable config uses all that have bounce mechanics on it. (on the chopping block)
- Parry refunds havent exprimented much
- 1 bug fixed this time. (silkthread 9 silk spam) 
- And a config for which slot, the silk slot binding puts it into. 


### PT6
- Icon.png now included
- Silkspeed anklets now use the same logic as needolin)
- Lifeblood changed to only give 1 mask upon conversion. (Lifeblood config removed.)
- Down attack into water fixed.
- Bind Sound issues, upon cancel are fixed.
- Weird audio bug with empty crest fixed
- Some minor value tweaks and minor code cleanup...

### PT7
- Added inherit half silk heart, which regenerates 0.5 silk, when you have no silkhearts. (bind 0.5 silk config feature removed)
- Set silk skill damage to 85% (removed config) 
- Spa's now have a minor interaction with half silk, like Sylphsong
- Wraiths and Clawmaidens can now take your last half silk
- Wisp Lantern can now use only 1 half silk. (config option off by default)
- Fixed charge attack voice lines not scaling with settings

### PT8
- Replaced the Placeholder Acid filter for half silk use, with a proper animation.
- Yellow and Blue slots are now locked.
- 4 memory lockets have been added across the map.
- Mt. Fay, Citadel Spa, Putrified Ducts, and Wisp/Underworks connection.
- Many refund fixes...

### PT9
- Eva wire walls, prison walls, coral tree pearls... and cages now refund silk.
- Finalized Memory locket locations...  (besides spa)
(new custom platform in ducts...)
- Moved MT fay one, and added a corpse behind it.
- Removed Parry refunds... (I don't like gameplay it would provide... and its too tedious to code)

### PT10
- Refunds should be finalized now...
- Only exeptions I can think of are red memory and new act 3 obsticles... But I think they should work out of the box...
- Also made it so half silk can start needlin and silkspeed anklets.
- Also minor up attack speed increase.

### PT11
- prevented platform disapears when exiting the same room in a save then rentering upon reloading...
- Locketplatform also fails to line up on the bottom side.
- default slashes can be turned around,and cause a minor visual issue.
- Charge attack might need a minor windup hitbox change...
- Down attack visual misalignment
- Down attack dash bug.
- Down attack drifters bug
- Mask trade on final mask... during dash attack and charge causes hornet to freeze...  Probably from the changes in hit trade behavior I did previously to make the last mask effects work
- Down attack minor hitbox adjustments? 
- Plus noodlevyrn hunters journal entry. 

### PT12
- Added Readme file...
- Improved down attacks logic... (to match vanilla)
- down attack and slashes now have the traditional followup dash to idle animation...
- Dashattack slight tweaks and cleanup.
- Damage effects redone.
- it should has removed the delay of the last mask effect...
- Basically the damage is treated the same way as normal, without the invicibility or the freeze.
- Some minor silk mechanical behavior changing... 
- Minor Dash attack improvments
- small typo fix. 

### PT13
- Added back in the half silk config bind
- Added in the noodlevyrm... WIP (a placeholder can be found in bonebottom)
- Fixed a minor up attack visual bug.
- Hunters journal has a placeholder image...

### PT14
- Now requires Assethelper
- noodlevyrm fully implemented, and has 3 spawn locations... Memorium, voltnest after voltwyrm death, and spa guarding memory locket.
- changed config defaults
- half silk after bind is true by default
- hold to bind is disabled by default.
- Up attack bugfix... (no more canceling by jumping)

### PT15
- Fixed up slash animation sometimes canceling...
- Fixed up slash cooldown reset on hitting the ground.
- Improved sound logic for noodlevyrm
- Fixed charge attack slightly accelerating fallspeed.
- Lowered charge attacks being effected by gravity to 25% when falling from it.. (you  can gain a slight amount of height with certain timing)

### PT16
- Changed charge attack gravity to 40% from 25*%... reduction. (to compensate for a change) (was closer to 30% before due to a bug)
- Added config to switch between 40%, 80% gravity reduction.
- Removed the 15% silk skill damage nerf.
- Reworked clawmirrors reducing its hitbox by 20%. (and fixed a damage bug)
- Fixed being able to pogo on sawtooth circlet...
- Rewrote the slash attack...
- To fix dash not being able to be used in the oposite direction you attack.
- the slash effect turning around, with you...
- Also making it more in line with vanilla attacks.
- Made the attack speed slightly faster... from 0.48 to 0.47 seconds per attack (maybe set recovery from 15 to 13...)
- Fixed audio issue with noodlvyrm

### Thunderstore release
Changes since final playtest.
- Minor hitbox improvement... (timing wise) extened by 2 frames to match slash effect... 
- plus expanded the hitbox by like 4%... (and improve slash animation placement)
- fixed the up slash not having a hitbox during the final slash effect frame.
- You no longer take damage during silkheart scenes and widow memory.
- Noodlevyrm has a proper journal picture
- New corpse assets in mt fay and ducts
- Weaver crest no longer gives 1% (for now) will be added back with chapel.
- Added 2 oversights to the refund system.
- Fixed a bug where the moveset reverts to hunters, when you enter on a save not on a bench.
- Cleaned up a tiny bit of code...
