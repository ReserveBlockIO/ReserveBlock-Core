using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Filters;

namespace ReserveBlockCore.Controllers
{
    public class ActionFilterController : ActionFilterAttribute 
    {
        public override void OnActionExecuting(ActionExecutingContext filterContext)
        {
            if(Globals.AlwaysRequireAPIPassword == true)
            {
                var somepass = filterContext.RouteData.Values.ContainsKey("somePassword");
                if (somepass)
                {
                    var pass = filterContext.RouteData.Values["somePassword"].ToString();
                    var passCheck = Globals.APIPassword.ToDecrypt(pass);
                    if (passCheck == pass && passCheck != "Fail")
                    {
                        //Allow command to process
                    }
                    else
                    {
                        filterContext.Result = new StatusCodeResult(403);
                    }
                }
                else
                {
                    filterContext.Result = new StatusCodeResult(403);
                }
            }

            if(Globals.IsWalletEncrypted)
            {
                if(Globals.EncryptPassword.Length == 0)
                {
                    var sendTx = filterContext.RouteData.Values.Values.Contains("SendTransaction");
                    var createADNR = filterContext.RouteData.Values.Values.Contains("CreateAdnr");
                    var transferADNR = filterContext.RouteData.Values.Values.Contains("TransferAdnr");
                    var deleteADNR = filterContext.RouteData.Values.Values.Contains("DeleteAdnr");
                    var importPrivKey = filterContext.RouteData.Values.Values.Contains("ImportPrivateKey");
                    var createSig = filterContext.RouteData.Values.Values.Contains("CreateSignature");
                    var castTopicVote = filterContext.RouteData.Values.Values.Contains("CastTopicVote");
                    var postNewTopic = filterContext.RouteData.Values.Values.Contains("PostNewTopic");
                    var mintSC = filterContext.RouteData.Values.Values.Contains("MintSmartContract");
                    var transferSC = filterContext.RouteData.Values.Values.Contains("TransferNFT");
                    var burn = filterContext.RouteData.Values.Values.Contains("Burn");
                    var evolve = filterContext.RouteData.Values.Values.Contains("Evolve");
                    var devolve = filterContext.RouteData.Values.Values.Contains("Devolve");
                    var evospec = filterContext.RouteData.Values.Values.Contains("EvolveSpecific");

                    if (sendTx || 
                        createADNR || 
                        transferADNR || 
                        deleteADNR || 
                        importPrivKey || 
                        createSig || 
                        castTopicVote || 
                        postNewTopic || 
                        mintSC || 
                        transferSC || 
                        burn || 
                        evolve || 
                        devolve ||
                        evospec)
                    {
                        filterContext.HttpContext.Response.StatusCode = 401;
                        filterContext.Result = new UnauthorizedObjectResult("You must type in your encryption password first!");
                    }
                    
                }
            }
        }
    }
}
