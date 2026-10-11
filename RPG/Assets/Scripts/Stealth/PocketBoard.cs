using UnityEngine;

namespace WorldRPG.Stealth
{
    /// <summary>
    /// The 3x3 "pocket matrix" opened once a lift succeeds (rules from StealthTest's pocket_pick_matrix prototype).
    /// Every pouch is face down. Probe a pouch to peek at the 2-3 pouches it is linked to (one-way links; the
    /// probed pouch itself stays covered). Take a pouch to grab whatever is in it. Each probe or take costs one move.
    /// Three valuables wins, a bell-tied purse adds 50 alarm, 100 alarm means the mark felt you.
    /// Pure logic, no drawing: <see cref="PickpocketHUD"/> renders it.
    /// </summary>
    public sealed class PocketBoard
    {
        public const int Count = 9;
        public const int MovesMax = 8;
        public const int Quota = 3;
        public const int TrapAlarm = 50;

        public enum Kind { Valuable, Trap, Junk }

        public struct Item
        {
            public string Name;
            public int Value;   // wen
            public Kind Kind;
        }

        public enum Result { Playing, Success, Alarmed, OutOfMoves }

        struct Pouch
        {
            public Item Item;
            public int[] Links;
            public bool Taken;
        }

        readonly Pouch[] _pouches = new Pouch[Count];

        public int MovesLeft { get; private set; }
        public int Valuables { get; private set; }
        public int Alarm { get; private set; }
        public int Value { get; private set; }
        public Result State { get; private set; }
        public bool Finished => State != Result.Playing;

        /// <summary>The pouch last probed (its links are revealed), or -1.</summary>
        public int Probed { get; private set; } = -1;
        /// <summary>The pouch last taken (its item stays shown until the next move), or -1.</summary>
        public int LastTaken { get; private set; } = -1;

        public void Deal(float wealth)
        {
            MovesLeft = MovesMax;
            Valuables = 0;
            Alarm = 0;
            Value = 0;
            Probed = -1;
            LastTaken = -1;
            State = Result.Playing;

            var items = new[]
            {
                Valuable("String of cash", 100, wealth),
                Valuable("Silver shard", 250, wealth),
                Valuable("Jade pendant", 400, wealth),
                Valuable("Gold hairpin", 600, wealth),
                new Item { Name = "Bell-tied purse", Kind = Kind.Trap },
                new Item { Name = "Bell-tied purse", Kind = Kind.Trap },
                new Item { Name = "Handkerchief", Kind = Kind.Junk },
                new Item { Name = "Rice cake", Kind = Kind.Junk },
                new Item { Name = "Pebble", Kind = Kind.Junk },
            };
            Shuffle(items);

            var pool = new int[Count - 1];
            for (int i = 0; i < Count; i++)
            {
                int n = 0;
                for (int k = 0; k < Count; k++)
                    if (k != i) pool[n++] = k;
                Shuffle(pool);
                var links = new int[Random.value < 0.6f ? 2 : 3];
                System.Array.Copy(pool, links, links.Length);
                _pouches[i] = new Pouch { Item = items[i], Links = links };
            }
        }

        public Item ItemAt(int i) => _pouches[i].Item;
        public bool IsTaken(int i) => _pouches[i].Taken;
        public int[] LinksOf(int i) => _pouches[i].Links;

        /// <summary>Is the content of pouch i visible right now (peeked through a probe, or just taken)?</summary>
        public bool IsShown(int i)
        {
            if (Finished) return true;
            if (Probed >= 0) return System.Array.IndexOf(_pouches[Probed].Links, i) >= 0;
            return LastTaken == i;
        }

        public bool Probe(int i)
        {
            if (Finished || _pouches[i].Taken) return false;
            MovesLeft--;
            Probed = i;
            LastTaken = -1;
            if (MovesLeft <= 0) State = Result.OutOfMoves;
            return true;
        }

        public bool Take(int i)
        {
            if (Finished || _pouches[i].Taken) return false;
            MovesLeft--;
            _pouches[i].Taken = true;
            Probed = -1;
            LastTaken = i;

            var item = _pouches[i].Item;
            if (item.Kind == Kind.Valuable)
            {
                Valuables++;
                Value += item.Value;
            }
            else if (item.Kind == Kind.Trap)
            {
                Alarm += TrapAlarm;
            }

            if (Valuables >= Quota) State = Result.Success;
            else if (Alarm >= 100) State = Result.Alarmed;
            else if (MovesLeft <= 0) State = Result.OutOfMoves;
            return true;
        }

        static Item Valuable(string name, int value, float wealth)
        {
            // Round to 10 wen so purses read like real coin counts.
            int wen = Mathf.Max(10, Mathf.RoundToInt(value * wealth * Random.Range(0.8f, 1.2f) / 10f) * 10);
            return new Item { Name = name, Value = wen, Kind = Kind.Valuable };
        }

        static void Shuffle<T>(T[] array)
        {
            for (int i = array.Length - 1; i > 0; i--)
            {
                int j = Random.Range(0, i + 1);
                (array[i], array[j]) = (array[j], array[i]);
            }
        }
    }
}
