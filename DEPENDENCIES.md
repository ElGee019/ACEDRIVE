are streetnames included
is there a minimap
are houses / businesses locations embedded



EVO uses presets within the same ks_bmw_m2_coupe car folder.
nenkai unpack


Reasons to drive

Storyline
	-history -- detective style

Jobs independent of calendar
	-Taxi Jobs
	-Delivery Jobs
	-Car hauling Jobs

Calendar-linked jobs
	-road test / car journalist
	-where is willy
	-cento miglia
	-Market Valuation

earning money for car collecting
earning xp for asynchronous competition
	

a-synchronous competition
	-XP
	-longest operating zone
	-highest shift earning
	-car magazine earnings

operating zone expansion
exploring - street knowledge	
friendship - every successful job results in friendship growing, every house has a citizen assigned

insurance
fuelstation
police
bank
mechanic
	-chances of breakdown (8 parts)
	-engine state

cardealer
employees when more than 1 car
	-carsharing
	-taxi jobs
	-delivery jobs

during drive: 
	-customer requests
	-car quizzes
	-passing car brand


Resource Drains
Breakdown
Accidents
Wrong Address
Failing a request
	


- Clock -> hunger (+1/hour; pancake pauses it)
- Clock -> fuel price (daily random walk, clamped 1.4–2.2) -> refuel cost -> job profit
- Clock -> insurance premium bill -> cash -> willingness to stay insured
- Clock -> store stock depletion (90-min ticks) -> see Town economy below
- Clock -> car-share nightly booking; employee arrivals; offline-job alarms
- Clock -> Calendar -> weekly content refresh (dealer stock, road-test car, roster, race)
- Calendar -> every weekday-gated story event: if the day is wrong, the event is simply absent


- Current car choice (Garage) -> top speed, tank size, trunk volume, brake distance
  -> which jobs are possible (trunk caps delivery load; tank caps distance)
- Driving km -> part wear (8 parts) -> breakdown chance -> tow fee + failed job + lost cargo
- Driving km -> engine health decay -> annual inspection -> engine can strand -> repair costs
- Driving km + car class -> valuation score -> Monday show result
- Accident roll (busy days, >=13 streets) -> repair cost (fraction of car value) -> insurance payout
- Dealer stock (Calendar week seed + car database + money) -> garage fleet -> everything above
- Job count -> car wash dirt accumulates -> dirty car -> taxi refusals + customer grumbles -> income
- Money -> wash, refuel, repairs, insurance, new cars -> keeps the above cycles running


- Taxi: map houses -> job pair; name jar -> passenger; hunger gate -> can start at all;
  fuel/cleanliness -> can finish; police streets -> fine vs tip; distance x class x rarity -> fare;
  tips -> friendship; fare -> money, XP, shift stats, bank interest, todo counters, zone growth.
- Delivery: owned hub (CredP) -> wholesale cart (escrow money); trunk volume -> cart size;
  shop POI + stock state -> destination + sell price; delivered -> shop stock +20, profit,
  CredP ramp -> bigger hub tiers; also dirt, XP, employee-mirror deliveries.
- Car hauling: brand loyalty 100% -> offer; dealer -> pickup leg; borrowed car -> drive leg;
  game minutes driven -> pay.
- Care packages: todo counters (from ALL jobs + money + XP + zone + cars) -> bag spawns in a
  shop -> delivery run -> pick-1-of-2 reward card.
- Reward cards -> cancel slices of other systems: Free Tank (fuel), Fine Waived (police),
  Half-Damage/Invincible (accidents), Heat-Free (breakdowns), Worker Bonus (employees).
- Bank: money parked -> interest credited per finished job -> receipts; daily streak -> bonus.
- XP & levels: jobs grant XP -> pay multipliers + job-type unlocks;
  driver rating -> insurance discount; street knowledge -> job quality; friendship -> tips.


Town economy

Stores bring citizens. If stores stocked, amount of citizens go up. If stores empty citizens leave. more citizens means more possible readers for car magazines, also means more demand for carsharing



Operating Zone growth

- Every few finished jobs -> one new street discovered ->
  new carwash/stores/fuel/dealer/bank/mechanic per placement interval ->
  more houses/shops -> more and better jobs.
- Real-world add-street: GPS -> employee drives there -> street joins the pool -> same chain.


Weekly stories

- All five depend on: Calendar gate + Clock + Driving + a system to score:
  
Cento Miglia (car class, borrowed physics, rival NPCs), 
Market Valuation (engine health, price, rivals),
Road Test (garage, store citizens as audience), 
Willy (shop list, friendship),
Grandma (houses, dialogue, pancake -> hunger pause).



Online (Firebase)

- Influences: standings names on receipts, rival lists, valuation entries, car rarity
  (global km -> rarity tier -> fare multiplier), car sprites, name reservation.
- Is influenced by: km, XP, zone, shift results (fire-and-forget pushes).



Loops (circular influence)

1. **Growth loop:** jobs -> new streets -> more shops/houses -> better jobs -> jobs.
2. **Town loop:** hub -> deliveries -> stock up -> citizens up -> car-share/road-test demand -> money -> higher hub tier.
3. **Wear spiral:** driving -> km -> wear -> breakdown -> failed job + fees -> less money -> deferred service -> more wear risk.
4. **Quest loop:** every system's counters -> todos -> care packages -> reward cards -> mute parts of those same systems.
5. **Hunger gate:** clock -> hunger -> job center locked -> buy/eat food (or Grandma pancakes) -> unlocked.




