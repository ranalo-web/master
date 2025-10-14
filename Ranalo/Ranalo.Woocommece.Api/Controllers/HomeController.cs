using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Http.Metadata;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using Ranalo.Woocommece.Api.Models;
using Ranalo.Woocommece.Api.Services;
using System.Collections.Generic;
using System.Diagnostics;
using System.Net;
using System.Net.Http.Headers;
using System.Text;

namespace Ranalo.Woocommece.Api.Controllers
{
    [Route("api/DataSync")]
    [ApiController]
    public class HomeController : Controller
    {
        //private readonly IHttpContextAccessor _contextAccessor;
        private readonly ISyncService _syncService;
        public HomeController(ISyncService syncService) 
        { 
            _syncService = syncService;
        }
        // GET: HomeController
        [HttpGet]
        [Route("SyncLogs")]
        [ProducesResponseType(typeof(DataSyncLog), 200)]  // Success
        public async Task<IActionResult> SyncLogs()
        {
            var lastLog = await _syncService.GetLastSycnLogDetails();
            if(lastLog != null)
            {
                return Ok();
            }

            return NoContent();
        }

        [HttpGet]
        [Route("SyncWooOrders")]
        [ProducesResponseType(typeof(List<WooOrder>), 200)]  // Success
        public async Task<ActionResult> WooOrders()
        {
            var mappedOrders = await _syncService.SyncWooOrders();
            return Ok(mappedOrders);
        }

        [HttpGet]
        [Route("SyncUpdateImagesWooOrders")]
        [ProducesResponseType(typeof(List<int>), 200)]  // Success
        public async Task<IActionResult> UpdateImages()
        {
            var updatedOrders = await _syncService.SyncUpdateImagesWooOrders();
            return Ok(updatedOrders);
        }

        [HttpGet]
        [Route("SyncUpdateNextOfKinWooOrders")]
        [ProducesResponseType(typeof(List<int>), 200)]  // Success
        public async Task<IActionResult> UpdateNextOfKin()
        {
            var updatedOrders = await _syncService.SyncUpdateNextOfKinWooOrders();
            return Ok(updatedOrders);
        }

        [HttpGet]
        [Route("SyncUpdateMetaDataWooOrders")]
        [ProducesResponseType(typeof(List<int>), 200)]  // Success
        public async Task<IActionResult> UpdateMetaData()
        {
            var updatedOrders = await _syncService.SyncUpdateMetaDataWooOrders();
            return Ok(updatedOrders);
        }


        [HttpGet]
        [Route("SyncPayments")]
        public async Task<IActionResult> KosePayments()
        {

            var grouped = await _syncService.SyncPayments();
            return Ok(grouped);

        }

        [HttpGet]
        [Route("SyncWooCustomers")]
        [ProducesResponseType(typeof(object), 200)]  // Success
        public ActionResult WooCustomers()
        {
            return Ok();
        }

        [HttpGet]
        [Route("DeviceUnlockPull")]
        [ProducesResponseType(typeof(object), 200)]  // Success
        public async Task<IActionResult> DeviceDetails()
        {
            var currentDevices = await _syncService.DeviceUnlockPull();

            return Ok(currentDevices);
        }


        [HttpGet]
        [Route("OrderById")]
        public async Task<IActionResult> WooGetOrderById(int orderId)
        {

            var mappedOrder = await _syncService.OrderById(orderId);
            return Ok(mappedOrder);

        }
    }
}

//Edward Guda
//2:56 PM
//### Patch update to the code
//# Initialize variables for pagination
//API_KEY < -"Token 8efccf09d4874f88ba2a62f5db8d8efc"
//base_url < -"https://app.nuovopay.com/dm/api/v1/devices.json"
//all_data < -list()
//page < -1

//# Fetch the first page to determine the column structure
//api < -GET(base_url, add_headers(Authorization = API_KEY),
//           query = list(limit = 100, page = page))

//if (status_code(api) != 200) { stop("Failed to fetch data. HTTP Status: ", status_code(api))}

//api_response < -content(api
