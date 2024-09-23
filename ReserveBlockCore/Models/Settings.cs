using ReserveBlockCore.Data;
using ReserveBlockCore.Utilities;

namespace ReserveBlockCore.Models
{
    public class Settings
    {
        public int Id { get; set; }
        public bool CorrectShutdown { get; set; }
        public DateTime? LastShutdown { get; set; }
        public DateTime? LastStartup { get; set; }
        public bool CalledToSeed { get; set; }


        #region Get Settings DB
        public static LiteDB.ILiteCollection<Settings>? GetSettingsDb()
        {
            try
            {
                var settings = DbContext.DB_Settings.GetCollection<Settings>(DbContext.RSRV_SETTINGS);
                return settings;
            }
            catch (Exception ex)
            {
                ErrorLogUtility.LogError(ex.ToString(), "Settings.GetSettings()");
                return null;
            }

        }

        #endregion

        public static Settings? GetSettings()
        {
            var settingsDb = GetSettingsDb();

            if(settingsDb != null)
            {
                var settingRec = settingsDb.Query().FirstOrDefault();
                if(settingRec == null)
                {
                    Settings setting = new Settings
                    {
                        CorrectShutdown = true,
                        LastShutdown = null,
                        LastStartup = null,
                    };

                    settingsDb.InsertSafe(setting);
                    settingRec = settingsDb.Query().FirstOrDefault();
                }

                return settingRec;
            }
            else
            {
                return null;
            }
        }

        public static async Task InitiateStartupUpdate()
        {
            var settingsDb = GetSettingsDb();

            if (settingsDb != null)
            {
                var settingRec = GetSettings();
                if(settingRec != null )
                {
                    settingRec.LastStartup = DateTime.Now;
                    settingRec.CorrectShutdown = false;

                    settingsDb.UpdateSafe(settingRec);
                }
                
            }
        }

        public static async Task InitiateShutdownUpdate()
        {
            var settingsDb = GetSettingsDb();

            if (settingsDb != null)
            {
                var settingRec = GetSettings();
                if(settingRec != null )
                {
                    settingRec.LastShutdown = DateTime.Now;
                    settingRec.CorrectShutdown = true;

                    settingsDb.UpdateSafe(settingRec);

                    DbContext.DB_Settings.Commit();

                    await Task.Delay(500);
                }
            }

            if(!string.IsNullOrEmpty(Globals.ValidatorAddress))
            {
                Globals.LastProofBlockheight = 999_999_999_999_999_999;
                bool run = true;
                while (run)
                {
                    foreach (var proof in Globals.WinningProofs)
                    {
                        if (proof.Value != null)
                        {
                            if (proof.Value.Address == Globals.ValidatorAddress)
                            {
                                await Task.Delay(10000);
                                continue;
                            }
                        }
                    }

                    run = false;
                }
            }
        }
    }
}
