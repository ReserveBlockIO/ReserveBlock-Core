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
        }
    }
}
