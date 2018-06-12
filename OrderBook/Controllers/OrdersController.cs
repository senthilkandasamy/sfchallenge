﻿using System;
using System.Collections.Generic;
using System.Fabric;
using System.Linq;
using System.Net;
using System.Threading;
using System.Threading.Tasks;
using Common;
using Microsoft.AspNetCore.Mvc;
using Microsoft.ServiceFabric.Data;
using Microsoft.ServiceFabric.Data.Collections;
using Microsoft.ApplicationInsights;
using Microsoft.ApplicationInsights.DataContracts;
using Microsoft.ApplicationInsights.Extensibility;
using Microsoft.Extensions.Logging;

namespace OrderBook.Controllers
{
    [Produces("application/json")]
    [Route("api/[controller]")]
    public class OrdersController : Controller
    {
        private readonly OrderBook orderBook;
        private TelemetryClient telemetry;

        public OrdersController(OrderBook orderBook)
        {
            this.orderBook = orderBook;

            // Get configuration from our PackageRoot/Config/Setting.xml file
            var context = FabricRuntime.GetActivationContext();
            var configurationPackage = context.GetConfigurationPackageObject("Config");
            var telemetryid = configurationPackage.Settings.Sections["ClusterConfig"].Parameters["TeamInstrumentation"].Value;
            this.telemetry = new TelemetryClient(new TelemetryConfiguration(telemetryid));
        }

        // GET api/orders
        [HttpGet]
        public async Task<IActionResult> GetAsync()
        {
            try
            {
                //log.Debug("1  OrdersController I am here! ");
                //System.Diagnostics.Trace.TraceError("2 OrdersController I am here!");


                // Send the event:
                this.telemetry.TrackTrace("GET api/orders");


                var asks = await this.orderBook.GetAsksAsync();
                var bids = await this.orderBook.GetBidsAsync();
                var asksCount = asks.Count;
                var bidsCount = bids.Count;
                var view = new OrderBookViewModel
                {
                    CurrencyPair = orderBook.PartitionName,
                    Asks = asks,
                    Bids = bids,
                    AsksCount = asksCount,
                    BidsCount = bidsCount,
                };
                return this.Json(view);
            }
            catch (FabricException)
            {
                return new ContentResult { StatusCode = 503, Content = "The service was unable to process the request. Please try again." };
            }
        }

        // DELETE api/orders
        [HttpDelete]
        public async Task<IActionResult> DeleteAsync()
        {
            try
            {
                await this.orderBook.ClearAllOrders();
                return this.Ok();
            }
            catch (FabricNotPrimaryException)
            {
                return new ContentResult { StatusCode = 410, Content = "The primary replica has moved. Please re-resolve the service." };
            }
            catch (FabricException)
            {
                return new ContentResult { StatusCode = 503, Content = "The service was unable to process the request. Please try again." };
            }
        }

        // GET api/orders/bids
        [Route("bids")]
        [HttpGet]
        public async Task<IActionResult> Bids()
        {
            try
            {
                // Send the event:
                this.telemetry.TrackTrace("GET api/orders/bids");

                var bids = await this.orderBook.GetBidsAsync();
                return this.Json(bids);
            }
            catch (InvalidAskException ex)
            {
                return new ContentResult { StatusCode = 400, Content = ex.Message };
            }
            catch (FabricException)
            {
                return new ContentResult { StatusCode = 503, Content = "The service was unable to process the request. Please try again." };
            }
        }

        // GET api/orders/asks
        [Route("asks")]
        [HttpGet]
        public async Task<IActionResult> Asks()
        {
            try
            {
                var bids = await this.orderBook.GetAsksAsync();
                return this.Json(bids);
            }
            catch (FabricException)
            {
                return new ContentResult { StatusCode = 503, Content = "The service was unable to process the request. Please try again." };
            }
        }

        // POST api/orders/bid
        [Route("bid")]
        [HttpPost]
        public async Task<IActionResult> Bid([FromBody] OrderRequestModel order)
        {
            try
            {
                if (order == null)
                {
                    return this.BadRequest("Null or invalid order");
                }
                if (order.Pair != orderBook.PartitionName)
                {
                    order.Pair = orderBook.PartitionName;
                }
                var orderId = await this.orderBook.AddBidAsync(order);
                return this.Ok(orderId);
            }
            catch (InvalidBidException ex)
            {
                ServiceEventSource.Current.ServiceException(orderBook.Context, "Invalid ask", ex);
                return new ContentResult { StatusCode = 400, Content = ex.Message };
            }
            catch (FabricNotPrimaryException ex)
            {
                ServiceEventSource.Current.ServiceException(orderBook.Context, "NotPrimary", ex);

                return new ContentResult { StatusCode = 410, Content = "The primary replica has moved. Please re-resolve the service." };
            }
            catch (FabricException ex)
            {
                ServiceEventSource.Current.ServiceException(orderBook.Context, "FabricException", ex);

                return new ContentResult { StatusCode = 503, Content = "The service was unable to process the request. Please try again." };
            }
            catch (MaxOrdersExceededException ex)
            {
                return new ContentResult { StatusCode = 429, Content = $"{ex.Message}" };
            }
        }

        // POST api/orders/ask
        [Route("ask")]
        [HttpPost]
        public async Task<IActionResult> Ask([FromBody] OrderRequestModel order)
        {
            try
            {
                if (order == null)
                {
                    return this.BadRequest("Null or invalid order");
                }
                if (order.Pair != orderBook.PartitionName)
                {
                    order.Pair = orderBook.PartitionName;
                }
                var orderId = await this.orderBook.AddAskAsync(order);
                return this.Ok(orderId);
            }
            catch (InvalidAskException ex)
            {
                ServiceEventSource.Current.ServiceException(orderBook.Context, "Invalid ask", ex);
                return new ContentResult { StatusCode = 400, Content = ex.Message };
            }
            catch (FabricNotPrimaryException ex)
            {
                ServiceEventSource.Current.ServiceException(orderBook.Context, "NotPrimary", ex);

                return new ContentResult { StatusCode = 410, Content = "The primary replica has moved. Please re-resolve the service." };
            }
            catch (FabricException ex)
            {
                ServiceEventSource.Current.ServiceException(orderBook.Context, "FabricException", ex);

                return new ContentResult { StatusCode = 503, Content = "The service was unable to process the request. Please try again." };
            }
            catch (MaxOrdersExceededException ex)
            {
                return new ContentResult { StatusCode = 429, Content = $"{ex.Message}" };
            }
        }
    }
}
