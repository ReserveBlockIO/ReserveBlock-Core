using ReserveBlockCore.Data;
using ReserveBlockCore.Models;

namespace ReserveBlockCore.Services
{
    public class RuleService
    {
        public static void ResetValidators()
        {
            var rules = ConfigData.GetConfigRules();
            var ruleResetFailCount = rules.FindOne(x => x.RuleName == "ResetValidators");

            var accounts = AccountData.GetAccounts();
            var accountValidators = accounts.FindAll().Where(x => x.IsValidating == true).ToList();

            if (ruleResetFailCount == null)
            {
                if(accountValidators.Count() > 0)
                {
                    accountValidators.ForEach(x => { 
                        x.IsValidating = false;
                        accounts.UpdateSafe(x);
                    });
                }
                ConfigRules cRule = new ConfigRules
                {
                    RuleName = "ResetValidators",
                    IsRuleApplied = true,
                };

                rules.InsertSafe(cRule);

                var validators = Validators.Validator.GetAll();
                var valList = validators.FindAll().ToList();
                if (valList.Count > 0)
                {
                    validators.DeleteAllSafe();
                    try
                    {
                        DbContext.DB_Peers.Checkpoint();
                    }
                    catch (Exception ex)
                    {
                        //error saving from db cache
                    }
                }
            }
            else
            {
                if (ruleResetFailCount.IsRuleApplied == false)
                {
                    if (accountValidators.Count() > 0)
                    {
                        accountValidators.ForEach(x => {
                            x.IsValidating = false;
                            accounts.UpdateSafe(x);
                        });
                    }

                    var validators = Validators.Validator.GetAll();
                    var valList = validators.FindAll().ToList();
                    if (valList.Count > 0)
                    {
                        validators.DeleteAllSafe();
                        try
                        {
                            DbContext.DB_Peers.Checkpoint();
                        }
                        catch (Exception ex)
                        {
                            //error saving from db cache
                        }
                    }
                }
            }
        }
        public static void ResetFailCounts()
        {
            var rules = ConfigData.GetConfigRules();
            var ruleResetFailCount = rules.FindOne(x => x.RuleName == "ResetFailCounts");

            if (ruleResetFailCount == null)
            {
                ConfigRules cRule = new ConfigRules
                {
                    RuleName = "ResetFailCounts",
                    IsRuleApplied = true,
                };

                rules.InsertSafe(cRule);

                var validators = Validators.Validator.GetAll();
                var valList = validators.FindAll().ToList();
                if (valList.Count > 0)
                {
                    foreach (var val in valList)
                    {
                        val.FailCount = 0;
                        validators.UpdateSafe(val);
                    }
                }
            }
            else
            {
                if (ruleResetFailCount.IsRuleApplied == false)
                {
                    var validators = Validators.Validator.GetAll();
                    var valList = validators.FindAll().ToList();
                    if (valList.Count > 0)
                    {
                        foreach (var val in valList)
                        {
                            val.FailCount = 0;
                            validators.UpdateSafe(val);
                        }
                    }
                }
            }
        }

        public static void RemoveOldValidators()
        {
            var validators = Validators.Validator.GetAll();
            var validatorList = validators.FindAll().Where(x => x.WalletVersion == null).ToList();

            if(validatorList.Count > 0)
            {
                validators.DeleteManySafe(x => x.WalletVersion == null);
            }
        }
    }
}
