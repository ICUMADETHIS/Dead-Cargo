using System;
using System.Collections.Generic;
using System.IO;
using DeadCargo.Cargo;
using UnityEngine;

namespace DeadCargo.Profile
{
    /// <summary>
    /// Loads, changes and saves the player's profile. Every change is saved at once, so quitting never loses progress.
    /// Saves are written to a temporary file and swapped in, so a crash mid-save cannot corrupt the profile.
    /// </summary>
    public static class ProfileStore
    {
        public const string FileName = "profile.json";

        private const string TempSuffix = ".tmp";
        private const string CorruptFileName = "profile.corrupt.json";

        private static PlayerProfile current;

        /// <summary>Raised after any change to the profile.</summary>
        public static event Action Changed;

        /// <summary>Full path of the save file.</summary>
        public static string FilePath => Path.Combine(Application.persistentDataPath, FileName);

        /// <summary>The loaded profile. Loads it from disk on first use.</summary>
        public static PlayerProfile Current
        {
            get
            {
                if (current == null)
                    current = Load();
                return current;
            }
        }

        /// <summary>Counts a finished run, makes it the last run and banks the given cargo into the stash.</summary>
        public static void RecordRun(RunSummary summary, IReadOnlyList<CargoStack> bankedCargo)
        {
            PlayerProfile profile = Current;
            profile.RecordRun(summary);

            if (bankedCargo != null)
            {
                foreach (CargoStack stack in bankedCargo)
                    profile.AddToStash(stack.Item.Id, stack.Quantity);
            }

            Commit();
        }

        /// <summary>Sells every unit of an item in the stash at its unit value.</summary>
        /// <returns>The credits earned.</returns>
        public static int Sell(CargoItemDefinition item)
        {
            int earned = SellWithoutSaving(item);
            if (earned > 0)
                Commit();

            return earned;
        }

        /// <summary>Sells every stash item the catalog knows. Unknown items stay in the stash.</summary>
        /// <returns>The credits earned.</returns>
        public static int SellAll(CargoCatalog catalog)
        {
            if (catalog == null)
                return 0;

            // Copied first, because selling removes entries.
            var entries = new List<StashEntry>(Current.Stash);
            int earned = 0;
            foreach (StashEntry entry in entries)
                earned += SellWithoutSaving(catalog.Find(entry.ItemId));

            if (earned > 0)
                Commit();

            return earned;
        }

        /// <summary>Adds or removes credits. For debug tools.</summary>
        public static void AddCredits(int amount)
        {
            Current.AddCredits(amount);
            Commit();
        }

        /// <summary>Empties the stash without paying for it. For debug tools.</summary>
        public static void ClearStash()
        {
            Current.ClearStash();
            Commit();
        }

        /// <summary>Replaces the profile with a fresh one and saves it.</summary>
        public static void ResetProfile()
        {
            current = new PlayerProfile();
            Commit();
        }

        /// <summary>Drops the loaded profile so the next access reads it from disk again.</summary>
        public static void Reload()
        {
            current = null;
            Changed?.Invoke();
        }

        // Statics survive play sessions when domain reload is off.
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void ResetStatics()
        {
            current = null;
            Changed = null;
        }

        private static int SellWithoutSaving(CargoItemDefinition item)
        {
            if (item == null)
                return 0;

            int quantity = Current.GetStashQuantity(item.Id);
            if (quantity <= 0)
                return 0;

            int earned = quantity * item.UnitValue;
            Current.RemoveFromStash(item.Id);
            Current.AddCredits(earned);
            return earned;
        }

        private static void Commit()
        {
            Save(Current);
            Changed?.Invoke();
        }

        private static PlayerProfile Load()
        {
            string path = FilePath;
            if (!File.Exists(path))
                return new PlayerProfile();

            try
            {
                var profile = JsonUtility.FromJson<PlayerProfile>(File.ReadAllText(path));
                if (profile == null)
                    throw new InvalidDataException("The save file is empty.");

                profile.Sanitise();
                return profile;
            }
            catch (Exception exception)
            {
                // Keeps the unreadable file so nothing is lost, and starts fresh.
                string backup = Path.Combine(Application.persistentDataPath, CorruptFileName);
                Debug.LogError($"Could not read the profile at '{path}'. It was kept as '{backup}' and a new profile was started. {exception.Message}");
                TryMove(path, backup);
                return new PlayerProfile();
            }
        }

        private static void Save(PlayerProfile profile)
        {
            string path = FilePath;
            string temp = path + TempSuffix;
            try
            {
                Directory.CreateDirectory(Path.GetDirectoryName(path));
                File.WriteAllText(temp, JsonUtility.ToJson(profile, true));

                if (File.Exists(path))
                    File.Replace(temp, path, null);
                else
                    File.Move(temp, path);
            }
            catch (Exception exception)
            {
                Debug.LogError($"Could not save the profile to '{path}'. Progress since the last save is only in memory. {exception.Message}");
            }
        }

        private static void TryMove(string from, string to)
        {
            try
            {
                if (File.Exists(to))
                    File.Delete(to);
                File.Move(from, to);
            }
            catch (Exception exception)
            {
                Debug.LogError($"Could not move '{from}' to '{to}'. {exception.Message}");
            }
        }
    }
}
