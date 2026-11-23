# 09 – Buying a trailer and keeping it

Last in the agreed order, and the hardest: the game's catalog and save know cars only.

## What the game and our mods give

- **The game's money** is an inventory item: `PlayerInventoryManager.Instance.MoneyItemId`, read with
  `Inventory.GetItemsCount(id)` and paid with `Inventory.TryRemoveItem(id, price)` – the mechanic's own purchase
  (`OnUpgradeButtonClickOrSubmit`); TireShop pays its tyres that way (`src/TireShop/Pebbles.cs` in oth-modes).
- **A mod's own save beside the game's:** TireShop keeps per profile (by name) and per car (by ItemId) what each car wears
  in `UserData/TireShop.json`, versioned, read once, written whole on each change, a bad file put aside; the game's save
  stays untouched, so removing the mod leaves it as it was.
- **A mod's own button in the game's UI:** TireShop puts its chains' icon on the tools panel; RealFuel draws its line
  above the gauges.
- **The game's catalog (ItemIds)** holds cars and their parts; a new car needs a clone of a donor (the long cars), which a
  trailer is not. The profile's name moved in the public beta of 17 November 2026 (`Profile.Name` → `LegacyName` /
  `ProfileSave.Header.Name`; GameCompat in oth-modes reads it on both).

## Design

**Owned trailers in the mod's own file**, `UserData/Trailers.json`: per profile, the trailers bought (type, a name, a
colour, its wheels), which one is hitched to which car (by ItemId) or parked where it was left (region, position – spec 06
parks anywhere). Nothing goes into the game's save; without the mod the save loads as it is.

**Its own wheels.** A bought trailer gets the wheels of the car it is first hitched to and keeps them (their size sets its
floor height); the Z drawbar adapts to each car it is hitched to (spec 04, 06).

**Buying.** At the mechanic, a «Trailers» panel (as TireShop's tyre page): the types with their prices in pebbles, paid
through the inventory like the mechanic's own upgrades; the price list in the file, the first trailer cheap.

**Keeping it.** After loading a profile or a portal, the trailer hitched to the car comes back behind it once the car
stands; a parked trailer comes back where it was left when the player returns to that region.

**Co-op.** Each player's trailers are theirs and their own file keeps them; in someone else's session only the hitched
state carries over (spec 05's property), and a trailer parked there is not saved – it comes back hitched next time, or at
the player's own last parking place in their own world.

**From the bench to a release.** TestTrailer stays the bench; the release mod (its name, keys, the garage panel instead
of the bench's keys for removing a trailer) is decided when 09 starts.

## Questions for the owner

- Prices: in line with the game's car upgrades (the winch, the roof tent)?
- Should a trailer carry cargo the game counts (the player's inventory items), or stay a look and a camp?
