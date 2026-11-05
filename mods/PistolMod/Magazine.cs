using System.Collections.Generic;
using System.Globalization;

namespace PistolMod
{
    internal static class Magazine
    {
        internal const int Capacity = 17;
        internal const string DataKey = "PistolMod.Rounds";
        internal static int Read(IDictionary<string, string> data, int capacity = Capacity)
        {
            if (!data.TryGetValue(DataKey, out string value) ||
                !int.TryParse(value, NumberStyles.Integer, CultureInfo.InvariantCulture, out int rounds))
            {
                return 0;
            }

            if (rounds < 0)
            {
                return 0;
            }

            return rounds > capacity ? capacity : rounds;
        }

        internal static void Write(IDictionary<string, string> data, int rounds, int capacity = Capacity)
        {
            int boundedRounds = rounds < 0 ? 0 : rounds > capacity ? capacity : rounds;
            data[DataKey] = boundedRounds.ToString(CultureInfo.InvariantCulture);
        }

        internal static bool TryFire(IDictionary<string, string> data, int capacity = Capacity)
        {
            int rounds = Read(data, capacity);
            if (rounds == 0)
            {
                return false;
            }

            Write(data, rounds - 1, capacity);
            return true;
        }

        internal static int ReloadAmount(int loaded, int reserve, int capacity = Capacity)
        {
            if (reserve <= 0 || loaded >= capacity)
            {
                return 0;
            }

            int boundedLoaded = loaded < 0 ? 0 : loaded;
            int needed = capacity - boundedLoaded;
            return reserve < needed ? reserve : needed;
        }
    }
}
