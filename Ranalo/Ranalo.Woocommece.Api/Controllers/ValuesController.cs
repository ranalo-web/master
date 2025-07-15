using Microsoft.AspNetCore.Mvc;
using System.Net.Http.Headers;
using System.Text;

// For more information on enabling Web API for empty projects, visit https://go.microsoft.com/fwlink/?LinkID=397860

namespace Ranalo.Woocommece.Api.Controllers
{
    [Route("api/[controller]")]
    [ApiController]
    public class ValuesController : ControllerBase
    {
        // GET: api/<ValuesController>
        [HttpGet]
        public IEnumerable<string> Get()
        {
            return new string[] { "value1", "value2" };
        }

        // GET api/<ValuesController>/5
        [HttpGet()]
        [Route("getdata")]
        public async Task<IActionResult> GetDataFromWoo()
        {
            var result = await SecuredApiGetRequestStringResponse();
            return Ok(result);
        }

        // POST api/<ValuesController>
        [HttpPost]
        public void Post([FromBody] string value)
        {
        }

        // PUT api/<ValuesController>/5
        [HttpPut("{id}")]
        public void Put(int id, [FromBody] string value)
        {
        }

        // DELETE api/<ValuesController>/5
        [HttpDelete("{id}")]
        public void Delete(int id)
        {
        }


        private async Task<string> SecuredApiGetRequestStringResponse(string url = "", string token = "")
        {
            var consumerKey = "ck_9bf5ade6a031f04b53bd31938d462895db40e00c";
            var consumerSecret = "cs_b2d5d61f3eae5093d85b7319905eb5942c614f99";
            var baseUrl = "https://ranalocredit.com/wp-json/wc/v3";
            var client = new HttpClient();
            client.DefaultRequestHeaders.Accept.Add(new MediaTypeWithQualityHeaderValue("application/json"));
            var authToken = Encoding.ASCII.GetBytes($"{consumerKey}:{consumerSecret}");
            client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Basic", Convert.ToBase64String(authToken));

            var iso8601UtcDate = DateTime.UtcNow.AddDays(-3).ToString("yyyy-MM-ddTHH:mm:ssZ"); ;

            var queryParams = new Dictionary<string, string>
                {
                    { "per_page", "100" },
                    { "page", "1" },
                    { "consumer_key", consumerKey },
                    { "consumer_secret", consumerSecret },
                    { "after",  iso8601UtcDate}
                };

            var queryString = await new FormUrlEncodedContent(queryParams).ReadAsStringAsync();
            var urlWithParams = $"{baseUrl}/orders?{queryString}";


            var response = await client.GetAsync(urlWithParams);

            response.EnsureSuccessStatusCode();

            return await response.Content.ReadAsStringAsync();
        }
    }
}
