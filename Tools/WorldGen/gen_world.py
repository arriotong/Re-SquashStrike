# Generates Assets/SquashSim/Career/World.cs and the calendar part of Tour.cs (TourCalendar.cs)
import datetime

# ---- regions ----
REG = ["EUR", "NAM", "SAM", "MENA", "AFR", "SAS", "EAS", "OCE"]
REG_NAME = {"EUR": "Europe", "NAM": "North America", "SAM": "South America", "MENA": "Middle East & North Africa",
            "AFR": "Africa", "SAS": "South Asia", "EAS": "East Asia", "OCE": "Oceania"}
# one-way economy flights between regions, US$ (same region: 180; same country: 90)
F = {}
def f(a, b, c): F[(a, b)] = c; F[(b, a)] = c
for r in REG: F[(r, r)] = 180
f("EUR","NAM",600); f("EUR","SAM",900); f("EUR","MENA",350); f("EUR","AFR",700); f("EUR","SAS",550); f("EUR","EAS",750); f("EUR","OCE",1300)
f("NAM","SAM",500); f("NAM","MENA",900); f("NAM","AFR",1100); f("NAM","SAS",1100); f("NAM","EAS",1000); f("NAM","OCE",1200)
f("SAM","MENA",1200); f("SAM","AFR",1000); f("SAM","SAS",1400); f("SAM","EAS",1500); f("SAM","OCE",1400)
f("MENA","AFR",500); f("MENA","SAS",350); f("MENA","EAS",650); f("MENA","OCE",1100)
f("AFR","SAS",650); f("AFR","EAS",900); f("AFR","OCE",1100)
f("SAS","EAS",400); f("SAS","OCE",800); f("EAS","OCE",600)

# ---- countries: code, name, region, strength (rating points on its pros' average), share of the world's pros, home city ----
C = [
 ("EGY","Egypt","MENA",4,16,"Cairo"), ("ENG","England","EUR",2,8,"London"), ("FRA","France","EUR",2,5,"Paris"),
 ("WAL","Wales","EUR",2,2,"Cardiff"), ("NZL","New Zealand","OCE",2,3,"Auckland"), ("PER","Peru","SAM",2,2,"Lima"),
 ("COL","Colombia","SAM",2,3,"Bogota"), ("MAS","Malaysia","EAS",2,4,"Kuala Lumpur"), ("HKG","Hong Kong","EAS",1,3,"Hong Kong"),
 ("PAK","Pakistan","SAS",1,3,"Islamabad"), ("USA","United States","NAM",0,6,"New York"), ("CAN","Canada","NAM",0,3,"Toronto"),
 ("AUS","Australia","OCE",0,3,"Sydney"), ("IND","India","SAS",0,5,"Mumbai"), ("SUI","Switzerland","EUR",0,2,"Zurich"),
 ("GER","Germany","EUR",0,2,"Hamburg"), ("JPN","Japan","EAS",-1,2,"Yokohama"), ("ESP","Spain","EUR",0,2,"Madrid"),
 ("RSA","South Africa","AFR",0,2,"Johannesburg"), ("SCO","Scotland","EUR",0,2,"Edinburgh"), ("IRL","Ireland","EUR",-1,1,"Dublin"),
 ("NED","Netherlands","EUR",-1,1,"Amsterdam"), ("BEL","Belgium","EUR",-1,1,"Nimy"), ("AUT","Austria","EUR",-2,1,"Vienna"),
 ("ITA","Italy","EUR",-2,1,"Milan"), ("CZE","Czech Republic","EUR",-1,1,"Prague"), ("POL","Poland","EUR",-2,1,"Warsaw"),
 ("SWE","Sweden","EUR",-1,1,"Stockholm"), ("FIN","Finland","EUR",-1,1,"Helsinki"), ("KEN","Kenya","AFR",-2,1,"Nairobi"),
 ("NGR","Nigeria","AFR",-2,1,"Lagos"), ("MEX","Mexico","NAM",-1,2,"Mexico City"), ("ARG","Argentina","SAM",-1,1,"Buenos Aires"),
 ("BRA","Brazil","SAM",-1,1,"Sao Paulo"), ("SGP","Singapore","EAS",-1,1,"Singapore"), ("KOR","South Korea","EAS",-1,1,"Seoul"),
 ("KUW","Kuwait","MENA",0,2,"Kuwait City"), ("QAT","Qatar","MENA",-1,1,"Doha"), ("JOR","Jordan","MENA",-1,1,"Amman"),
 ("IRN","Iran","MENA",-1,1,"Tehran"),
 # host countries only (no players of their own in the roster)
 ("HUN","Hungary","EUR",-2,0,"Budapest"), ("CHN","China","EAS",-2,0,"Shanghai"), ("BER","Bermuda","NAM",-2,0,"Devonshire"),
 ("UZB","Uzbekistan","SAS",-2,0,"Tashkent"), ("SRI","Sri Lanka","SAS",-2,0,"Colombo"), ("PHI","Philippines","EAS",-2,0,"Manila"),
 ("POR","Portugal","EUR",-2,0,"Funchal"),
]
CR = {c[0]: c[2] for c in C}

# ---- cities: name, country, hotel a night (US$), map position (longitude, latitude) ----
CITIES = [
 ("Cairo","EGY",70,31.2,30.0), ("Giza","EGY",80,31.1,30.0), ("El Gouna","EGY",120,33.7,27.4), ("Alexandria","EGY",70,29.9,31.2),
 ("London","ENG",220,-0.1,51.5), ("Manchester","ENG",140,-2.2,53.5), ("Birmingham","ENG",140,-1.9,52.5), ("Nottingham","ENG",110,-1.2,53.0),
 ("Winchester","ENG",130,-1.3,51.1), ("Stourbridge","ENG",100,-2.1,52.5),
 ("Paris","FRA",210,2.35,48.9), ("Quimper","FRA",100,-4.1,48.0), ("Brest","FRA",100,-4.5,48.4), ("Nantes","FRA",120,-1.55,47.2),
 ("Cardiff","WAL",120,-3.2,51.5), ("Edinburgh","SCO",150,-3.2,55.95), ("Dublin","IRL",170,-6.25,53.35),
 ("Amsterdam","NED",190,4.9,52.4), ("Nimy","BEL",100,3.95,50.5), ("Zurich","SUI",240,8.55,47.4), ("Vienna","AUT",150,16.4,48.2),
 ("Milan","ITA",170,9.2,45.5), ("Prague","CZE",110,14.4,50.1), ("Brno","CZE",90,16.6,49.2), ("Warsaw","POL",100,21.0,52.2),
 ("Stockholm","SWE",170,18.1,59.3), ("Helsinki","FIN",160,24.9,60.2), ("Hamburg","GER",160,10.0,53.55), ("Madrid","ESP",150,-3.7,40.4),
 ("Budapest","HUN",110,19.05,47.5), ("Funchal","POR",120,-16.9,32.65),
 ("New York","USA",280,-74.0,40.7), ("Philadelphia","USA",200,-75.2,40.0), ("Redwood City","USA",230,-122.2,37.5),
 ("Charlottesville","USA",150,-78.5,38.0), ("Cleveland","USA",150,-81.7,41.5), ("Bloomfield Hills","USA",170,-83.25,42.6),
 ("Houston","USA",160,-95.4,29.8), ("Pittsburgh","USA",160,-80.0,40.4), ("Washington","USA",230,-77.0,38.9),
 ("Chicago","USA",210,-87.6,41.9), ("Boston","USA",230,-71.1,42.35), ("Johns Creek","USA",140,-84.2,34.0),
 ("The Plains","USA",160,-77.8,38.9), ("Columbia","USA",140,-76.9,39.2),
 ("Toronto","CAN",180,-79.4,43.7), ("Vancouver","CAN",190,-123.1,49.3), ("Calgary","CAN",160,-114.1,51.05), ("London, Ontario","CAN",130,-81.25,43.0),
 ("Mexico City","MEX",110,-99.1,19.4), ("Devonshire","BER",260,-64.7,32.3),
 ("Bogota","COL",80,-74.1,4.7), ("Lima","PER",90,-77.0,-12.05), ("Sao Paulo","BRA",110,-46.6,-23.55), ("Buenos Aires","ARG",100,-58.4,-34.6),
 ("Doha","QAT",180,51.5,25.3), ("Kuwait City","KUW",160,47.98,29.4), ("Amman","JOR",100,35.9,31.95), ("Tehran","IRN",80,51.4,35.7),
 ("Johannesburg","RSA",100,28.05,-26.2), ("Cape Town","RSA",120,18.4,-33.9), ("Nairobi","KEN",90,36.8,-1.3), ("Lagos","NGR",100,3.4,6.45),
 ("Mumbai","IND",110,72.9,19.1), ("Jaipur","IND",70,75.8,26.9), ("Bengaluru","IND",80,77.6,12.95), ("Chennai","IND",80,80.3,13.1),
 ("Islamabad","PAK",70,73.05,33.7), ("Lahore","PAK",60,74.35,31.55), ("Karachi","PAK",60,67.0,24.85), ("Colombo","SRI",70,79.85,6.9),
 ("Tashkent","UZB",70,69.25,41.3),
 ("Kuala Lumpur","MAS",90,101.7,3.15), ("Petaling Jaya","MAS",80,101.6,3.1), ("Seremban","MAS",60,101.9,2.7), ("Penang","MAS",70,100.3,5.4),
 ("Singapore","SGP",220,103.8,1.35), ("Hong Kong","HKG",220,114.2,22.3), ("Shanghai","CHN",150,121.5,31.2),
 ("Yokohama","JPN",150,139.6,35.45), ("Tokyo","JPN",180,139.7,35.7), ("Seoul","KOR",140,127.0,37.55), ("Manila","PHI",80,121.0,14.6),
 ("Sydney","AUS",190,151.2,-33.9), ("Brisbane","AUS",160,153.0,-27.5), ("Melbourne","AUS",170,145.0,-37.8), ("Devonport","AUS",110,146.35,-41.2),
 ("Bega","AUS",110,149.8,-36.7), ("Auckland","NZL",160,174.8,-36.85), ("Christchurch","NZL",140,172.6,-43.5),
]
CITY = {c[0]: c for c in CITIES}
for c in C: assert c[5] in CITY, c

# ---- the World Tour, 2024-25 (men's), start dates ----
START = datetime.date(2024, 7, 29)
WT = [
 ("Egyptian Open","Giza","Diamond","2024-08-30"), ("Paris Squash","Paris","Platinum","2024-09-15"),
 ("NASH Cup","London, Ontario","Copper","2024-09-17"), ("Budapest Open","Budapest","Copper","2024-09-22"),
 ("Charlottesville Open","Charlottesville","Copper","2024-09-24"), ("Qatar Classic","Doha","Platinum","2024-09-30"),
 ("Open Squash Classic","New York","Bronze","2024-10-06"), ("Silicon Valley Open","Redwood City","Gold","2024-10-11"),
 ("Vancouver Open","Vancouver","Copper","2024-10-16"), ("U.S. Open","Philadelphia","Platinum","2024-10-19"),
 ("Toronto Classic","Toronto","Copper","2024-10-27"), ("China Open","Shanghai","Silver","2024-10-30"),
 ("Czech Open","Brno","Copper","2024-10-31"), ("London Open","London","Copper","2024-11-06"),
 ("Malaysia Squash Cup","Petaling Jaya","Bronze","2024-11-12"), ("Singapore Open","Singapore","Gold","2024-11-19"),
 ("QSF No.5","Doha","Bronze","2024-11-20"), ("Cape Town Open","Cape Town","Copper","2024-11-26"),
 ("Hong Kong Open","Hong Kong","Platinum","2024-12-02"), ("Squash In The Land","Cleveland","Silver","2025-01-14"),
 ("Tournament of Champions","New York","Platinum","2025-01-23"), ("Motor City Open","Bloomfield Hills","Silver","2025-02-04"),
 ("Texas Open","Houston","Gold","2025-02-12"), ("QSF No.2","Doha","Bronze","2025-02-18"),
 ("Canadian Men's Open","Calgary","Bronze","2025-02-24"), ("Pittsburgh Open","Pittsburgh","Silver","2025-02-24"),
 ("New Zealand Open","Christchurch","Silver","2025-03-04"), ("Australian Open","Brisbane","Gold","2025-03-11"),
 ("German Open","Hamburg","Bronze","2025-03-19"), ("Indian Open","Mumbai","Copper","2025-03-24"),
 ("London Championships","London","Gold","2025-03-25"), ("Manchester Open","Manchester","Bronze","2025-04-02"),
 ("El Gouna International","El Gouna","Platinum","2025-04-12"), ("Grasshopper Cup","Zurich","Gold","2025-04-22"),
 ("Squash on Fire Open","Washington","Bronze","2025-04-23"), ("Bermuda Open","Devonshire","Copper","2025-04-29"),
 ("World Championship","Chicago","WorldChampionship","2025-05-09"), ("British Open","Birmingham","Diamond","2025-05-27"),
 ("Tour Finals","Cairo","WorldTourFinals","2025-06-17"),
]
def week(d): return (datetime.date.fromisoformat(d) - START).days // 7

# ---- the Challengers: 77, at real venues, spread over the season's weeks (none in the Finals' and the off-season's weeks) ----
CH_TIERS = ["Challenger15"]*8 + ["Challenger12"]*8 + ["Challenger9"]*15 + ["Challenger6"]*20 + ["Challenger3"]*26
CH_VENUES = ["Cairo","Jaipur","Devonport","Johns Creek","Seremban","Tashkent","Winchester","Mumbai","Helsinki","Columbia","Nimy",
 "Johannesburg","Colombo","Sao Paulo","Stourbridge","Quimper","Bengaluru","Hong Kong","Manila","Brest","The Plains","Yokohama",
 "Giza","Dublin","Nantes","Islamabad","Chicago","Prague","Funchal","Melbourne","Boston","Lahore","Alexandria","Kuala Lumpur",
 "Bogota","Lima","Mexico City","Edinburgh","Cardiff","Nottingham","Amsterdam","Vienna","Milan","Warsaw","Stockholm","Madrid",
 "Kuwait City","Amman","Seoul","Tokyo","Penang","Chennai","Karachi","Auckland","Bega","Nairobi","Lagos","Buenos Aires","Toronto",
 "Calgary","Cape Town","Doha","Tehran","Hamburg","Sydney","Singapore","Christchurch","Brisbane","Manchester","Philadelphia",
 "Vancouver","Shanghai","Kuala Lumpur","Cairo","Giza","Mumbai","London"]
assert len(CH_TIERS) == 77 and len(CH_VENUES) == 77, (len(CH_TIERS), len(CH_VENUES))
for v in CH_VENUES: assert v in CITY, v
# spread: the Challenger weeks are 0..45; interleave tiers so every stretch has small and bigger ones
import random
rng = random.Random(2025)
order = list(range(77)); rng.shuffle(order)
tiers = [CH_TIERS[i] for i in order]
CH = []
for k in range(77):
    wk = 1 + (k * 45) // 77                    # weeks 1..45, ~1.7 a week
    venue = CH_VENUES[k]
    CH.append((venue + (" Open" if CH_TIERS.index(tiers[k]) < 16 else " Challenger"), venue, tiers[k], wk))

def cs(s): return '"' + s.replace('"', '\\"') + '"'

out = []
out.append("""// ===========================================================
// Assets/SquashSim/Career/World.cs
// P7 phase 3 (2026-10-26): the world the tour travels - regions, the
// countries (how strong their squash is, their share of the world's pros,
// their home city), the cities (a hotel's price, a place on the map) and
// the flights between regions. Generated by gen_world.py from one table;
// edit the table, not this file. Presentation of money only: the sim's
// physics never reads it.
//
//   strength: rating points on the country's pros' average - Egypt the
//   strongest (+4), then England, France, Wales, New Zealand, Peru,
//   Colombia, Malaysia (+2), Hong Kong and Pakistan (+1)...
// ===========================================================

using System.Collections.Generic;

namespace SquashSim.Career
{
    public sealed class Country { public string Code, Name, Region, Home; public int Strength, Share; }
    public sealed class City { public string Name, Country; public int Hotel; public float Lon, Lat; }

    public static class World
    {
        public static readonly string[] Regions = { """ + ", ".join(cs(r) for r in REG) + """ };
        public static readonly string[] RegionNames = { """ + ", ".join(cs(REG_NAME[r]) for r in REG) + """ };

        static readonly int[,] Flights =
        {""")
for a in REG:
    out.append("            { " + ", ".join(str(F[(a, b)]).rjust(4) for b in REG) + " },   // " + a)
out.append("""        };

        public static readonly Country[] Countries =
        {""")
for c in C:
    out.append("            new Country { Code = %s, Name = %s, Region = %s, Strength = %d, Share = %d, Home = %s }," % (cs(c[0]), cs(c[1]), cs(c[2]), c[3], c[4], cs(c[5])))
out.append("""        };

        public static readonly City[] Cities =
        {""")
for c in CITIES:
    out.append("            new City { Name = %s, Country = %s, Hotel = %d, Lon = %sf, Lat = %sf }," % (cs(c[0]), cs(c[1]), c[2], c[3], c[4]))
out.append("""        };

        static Dictionary<string, Country> _country;
        static Dictionary<string, City> _city;

        public static Country CountryOf(string code)
        {
            if (_country == null) { _country = new Dictionary<string, Country>(); foreach (var c in Countries) _country[c.Code] = c; }
            return code != null && _country.TryGetValue(code, out var x) ? x : null;
        }

        public static City CityOf(string name)
        {
            if (_city == null) { _city = new Dictionary<string, City>(); foreach (var c in Cities) _city[c.Name] = c; }
            return name != null && _city.TryGetValue(name, out var x) ? x : null;
        }

        public static string RegionOf(City c) { var k = c != null ? CountryOf(c.Country) : null; return k != null ? k.Region : "EUR"; }
        public static string RegionName(string region) { int i = System.Array.IndexOf(Regions, region); return i >= 0 ? RegionNames[i] : region; }

        /// <summary>A country's home city (where a player of that nation starts); London if unknown.</summary>
        public static string HomeOf(string nat) { var c = CountryOf(nat); return c != null ? c.Home : "London"; }

        /// <summary>A flight from one city to another, one way (US$): 0 in the same city, 90 in the same country, then by
        /// region (180 within one, up to 1,500 across the world).</summary>
        public static int Flight(string from, string to)
        {
            if (from == to) return 0;
            var a = CityOf(from); var b = CityOf(to);
            if (a == null || b == null) return 600;
            if (a.Country == b.Country) return 90;
            int i = System.Array.IndexOf(Regions, RegionOf(a)), j = System.Array.IndexOf(Regions, RegionOf(b));
            return i < 0 || j < 0 ? 600 : Flights[i, j];
        }
    }
}""")
open(r'C:\SQUASH\re-SquashStrike\Assets\SquashSim\Career\World.cs', 'w', encoding='utf-8').write("\n".join(out) + "\n")

# ---- the calendar ----
cal = []
for name, city, tier, d in WT: cal.append((week(d), name, city, tier))
for name, city, tier, wk in CH: cal.append((wk, name, city, tier))
cal.sort(key=lambda e: (e[0], -["Challenger3","Challenger6","Challenger9","Challenger12","Challenger15","Copper","Bronze","Silver","Gold","Platinum","Diamond","WorldChampionship","WorldTourFinals"].index(e[3])))
print("weeks used:", min(e[0] for e in cal), "..", max(e[0] for e in cal), " events:", len(cal))
from collections import Counter
print(Counter(e[3] for e in cal))
lines = ["""// ===========================================================
// Assets/SquashSim/Career/TourCalendar.cs
// P7 phase 3: the season's events, week by week - the 39 graded events
// of the real 2024-25 PSA men's World Tour (their weeks, cities, tiers;
// sponsors' names left off) and 77 Challengers at real Challenger
// venues. Week 0 starts 29 July; the Tour Finals are in week 46; weeks
// 47-51 are the off-season. Generated by gen_world.py.
// ===========================================================

namespace SquashSim.Career
{
    public sealed class TourEvent { public int Week; public string Name, City; public Tier Tier; }

    public static partial class Tour
    {
        public static readonly TourEvent[] Calendar =
        {"""]
for wk, name, city, tier in cal:
    lines.append("            new TourEvent { Week = %d, Name = %s, City = %s, Tier = Tier.%s }," % (wk, cs(name), cs(city), tier))
lines.append("""        };
    }
}""")
open(r'C:\SQUASH\re-SquashStrike\Assets\SquashSim\Career\TourCalendar.cs', 'w', encoding='utf-8').write("\n".join(lines) + "\n")
print("ok")
