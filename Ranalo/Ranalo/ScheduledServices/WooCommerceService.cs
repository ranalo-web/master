using System.Globalization;
using System.Security.Policy;
using WooCommerceNET;
using WooCommerceNET.WooCommerce.v3;

namespace Ranalo.ScheduledServices
{
    public class WooCommerceService : IWooCommerceService
    {
        public async Task<List<Order>> GetOrders()
        {

            //    "https://ranalocredit.com/wp-json/wc/v3",
            //    "ck_9bf5ade6a031f04b53bd31938d462895db40e00c",
            //    "cs_b2d5d61f3eae5093d85b7319905eb5942c614f99"
            RestAPI rest = new RestAPI("https://ranalocredit.com/wp-json/wc/v3", "ck_9bf5ade6a031f04b53bd31938d462895db40e00c", "cs_b2d5d61f3eae5093d85b7319905eb5942c614f99");
            WCObject wc = new WCObject(rest);
            var url = "https://ranalocredit.com/wp-json/wc/v3/orders?per_page=10&page=1&consumer_key=ck_0090896477d37b5ce6e006eabd7f579aacb1a97f&consumer_secret=cs_2969d990e2967d37aab8078572ee30020417467f&modified_after=2025-12-02T15%3A41%3A20Z&orderby=modified&order=asc";
            using var http = new HttpClient();

            http.DefaultRequestHeaders.Add("User-Agent",
                "Mozilla/5.0 (Windows NT 10.0; Win64; x64) AppleWebKit/537.36 (KHTML, like Gecko) Chrome/120.0.0.0 Safari/537.36");
            http.DefaultRequestHeaders.Add("Accept", "text/html,application/xhtml+xml,application/xml;q=0.9,*/*;q=0.8");
            http.DefaultRequestHeaders.Add("Accept-Language", "en-US,en;q=0.5");
            http.DefaultRequestHeaders.Add("Accept-Encoding", "gzip, deflate, br");
            http.DefaultRequestHeaders.Add("Connection", "keep-alive");
            http.DefaultRequestHeaders.Add("Upgrade-Insecure-Requests", "1");
            http.DefaultRequestHeaders.Add("Cache-Control", "no-cache");

            var response = await http.GetAsync(url);
            var content = await response.Content.ReadAsStringAsync();


            //Get all products
            var products = await wc.Order.GetAll();

            //return new List<Order>();
            return products;

            ////Add new product
            //Product p = new Product()
            //{
            //    name = "test product 8",
            //    title = "test product 8",
            //    description = "test product 8",
            //    price = 8.0M
            //};
            //await wc.Product.Add(p);

            ////Update products with new values
            //await wc.Product.Update(128, new Product { name = "test 9" });

            ////Update products with Null values
            //await wc.Product.UpdateWithNull(128, new { name = "test 9", weight = "", date_on_sale_from = "", date_on_sale_to = "" });

            ////Delete product
            //await wc.Product.Delete(128);

            ////Use parameters
            //var p = await wc.Product.GetAll(new Dictionary<string, string>() {
            //    { "include", "10, 11, 12, 13, 14, 15" },
            //    { "per_page", "15" } });


            ////Batch add/update/delete
            //CustomerBatch cb = new CustomerBatch();

            //List<Customer> create = new List<Customer>();
            //create.Add(new Customer()
            //{
            //    first_name = "first",
            //    last_name = "last",
            //    email = "first@lastsss.com",
            //    username = "firstnlast",
            //    password = "12345"
            //});

            //List<Customer> update = new List<Customer>();
            //update.Add(new Customer()
            //{
            //    id = 4,
            //    last_name = "xu2"
            //});

            //List<int> delete = new List<int>() { 8 };
            //cb.create = create;
            //cb.update = update;
            //cb.delete = delete;

            //var c = await wc.Customer.UpdateRange(cb);
        }
    }
}
