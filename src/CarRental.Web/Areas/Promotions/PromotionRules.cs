using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using CarRental.Web.Areas.Billing.Data;
namespace CarRental.Web.Areas.Promotions;
public static class PromotionRules {
 public static string State(Promotion p,DateTime now) => p.Status=="DaKetThuc"||now>=p.End?"DaKetThuc":p.Status=="TamNgung"?"TamNgung":now<p.Start?"SapApDung":"DangApDung";
 public static string Revision(Promotion p,IEnumerable<PromotionCondition> rules) => Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(JsonSerializer.Serialize(new {p.Id,p.Name,p.Kind,p.Value,p.Start,p.End,p.Status,p.ActualEnd,p.Description,Rules=rules.OrderBy(x=>x.Id).Select(x=>new {x.Id,x.Kind,x.Group,x.Season,x.Start,x.End})}))));
}
