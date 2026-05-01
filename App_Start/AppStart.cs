using System.Web;
using System.Web.Mvc;
using System.Web.Routing;
using System.Web.Optimization;
using StoreInventory.DAL;

namespace StoreInventory
{
    // ── Routes ───────────────────────────────────────────────────────────────
    public class RouteConfig
    {
        public static void RegisterRoutes(RouteCollection routes)
        {
            routes.IgnoreRoute("{resource}.axd/{*pathInfo}");
            routes.MapRoute(
                name: "Default",
                url: "{controller}/{action}/{id}",
                defaults: new { controller = "Home", action = "Index", id = UrlParameter.Optional }
            );
        }
    }

    // ── Global Filters ───────────────────────────────────────────────────────
    public class FilterConfig
    {
        public static void RegisterGlobalFilters(GlobalFilterCollection filters)
        {
            filters.Add(new HandleErrorAttribute());
            filters.Add(new SessionAuthFilter());
        }
    }

    // ── Bundles ───────────────────────────────────────────────────────────────
    public class BundleConfig
    {
        public static void RegisterBundles(BundleCollection bundles)
        {
            bundles.Add(new ScriptBundle("~/bundles/jquery")
                .Include("~/Scripts/jquery-{version}.js"));

            bundles.Add(new ScriptBundle("~/bundles/jqueryval")
                .Include("~/Scripts/jquery.validate*"));

            bundles.Add(new ScriptBundle("~/bundles/bootstrap")
                .Include("~/Scripts/bootstrap.bundle.js"));

            bundles.Add(new ScriptBundle("~/bundles/chartjs")
                .Include("~/Scripts/Chart.min.js"));

            bundles.Add(new StyleBundle("~/Content/css")
                .Include("~/Content/css/site.css"));

            BundleTable.EnableOptimizations = false;
        }
    }

    // ── Session-based Auth Filter ─────────────────────────────────────────────
    public class SessionAuthFilter : ActionFilterAttribute
    {
        private static readonly string[] _publicActions =
            { "Login", "Logout" };

        public override void OnActionExecuting(ActionExecutingContext ctx)
        {
            var controller = ctx.ActionDescriptor.ControllerDescriptor.ControllerName;
            var action     = ctx.ActionDescriptor.ActionName;

            // Allow public pages
            if (controller == "Account") { base.OnActionExecuting(ctx); return; }

            // Redirect unauthenticated users
            if (SessionHelper.UserId == 0)
            {
                ctx.Result = new RedirectResult("~/Account/Login");
                return;
            }

            // Admin-only areas
            if (controller == "Users" && !SessionHelper.IsAdmin)
            {
                ctx.Result = new RedirectResult("~/Home/Index");
                return;
            }

            // Manager+ only areas
            if ((controller == "PurchaseOrders" && action == "Create")
                && !SessionHelper.IsMgr)
            {
                ctx.Result = new RedirectResult("~/Home/Index");
                return;
            }

            base.OnActionExecuting(ctx);
        }
    }
}
