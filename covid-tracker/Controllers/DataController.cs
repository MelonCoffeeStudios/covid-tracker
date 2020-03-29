﻿using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Security.Claims;
using System.Text;
using System.Threading.Tasks;
using covid_tracker.Data;
using covid_tracker.Data.Dto;
using covid_tracker.Data.Models;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Components.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using NetTopologySuite.Geometries;
using Newtonsoft.Json;

namespace covid_tracker.Controllers
{

    /*
     *   /Data    = Index
     *   /Data/Upload   =  Upload
     * 
     */
     [Authorize]
    public class DataController : Controller
    {
        public ApplicationDbContext ctx { get; set; }
        public ApplicationUser User { get; set; }

        public DataController(ApplicationDbContext _ctx, IHttpContextAccessor httpContextAccessor)
        {
            ctx = _ctx;
            var uid = httpContextAccessor.HttpContext.User.FindFirst(ClaimTypes.NameIdentifier).Value;
            if (uid != null)
            {
                User = ctx.Users.FirstOrDefault(x => x.Id == uid);
            }
            User.AgeInYears = 22;
            ctx.SaveChanges();
        }

        public IActionResult Index()
        {
            var model = ctx.DataSet.Where(x => x.User == User);
            return View(model);
        }

        public IActionResult Upload()
        {
            return View();
        }


        public IActionResult Details(int id)
        {
            //var model = ctx.DataSet.Include(x=> x.DataPoints).FirstOrDefault(x => x.Id == id);
            var model = ctx.DataSet.FirstOrDefault(x => x.Id == id);
            return View(model);
        }

        public async Task<IActionResult> Calculate()
        {
            Dictionary<DataPoint, List<DataPoint>> dict = new Dictionary<DataPoint, List<DataPoint>>();
            int i = 0;
            foreach(var point in ctx.DataPoints.Where(x=>x.User == User))
            {
                var otherPoints = ctx.DataPoints.Where(x =>
                    x.User != User &&
                    x.Timestamp > point.Timestamp.AddMinutes(-1) &&
                    x.Timestamp < point.Timestamp.AddMinutes(1) && 
                    x.Location.IsWithinDistance(point.Location, 10)
                    ).ToList();
                if(otherPoints.Count != 0)
                {
                    dict.Add(point, otherPoints);
                }
                i++;

                

            }
            return Ok(JsonConvert.SerializeObject(dict));
        }

        [RequestFormLimits(ValueLengthLimit = int.MaxValue, MultipartBodyLengthLimit = int.MaxValue)]
        [DisableRequestSizeLimit]
        [Consumes("multipart/form-data")] // for Zip files with form data
        [HttpPost]
        public async Task<IActionResult> UploadFile(List<IFormFile> files)
        {
            long size = files.Sum(f => f.Length);

            var data = await ReadAsStringAsync(files[0]);

            DtoLocationHistoryFormat obj = JsonConvert.DeserializeObject<DtoLocationHistoryFormat>(data);

            var newDataset = new DataSet()
            {
                Name = "New DataSet - " + DateTime.Now.ToString(),
                User = User,
                DataPoints = new List<DataPoint>()
            };

            var cutOffDate = new DateTime(2019, 11, 1);

            ctx.DataSet.Add(newDataset);
            ctx.SaveChanges();

            Console.WriteLine("Total entries: " + obj.Locations.Count());

            var locations = obj.Locations.Where(x => x.Timestamp >= cutOffDate).ToList();

            foreach(var point in locations)
            {
                var loc = GeneralizePoint(new NetTopologySuite.Geometries.Point(point.LongitudeE7 / 10000000, point.LatitudeE7 / 10000000) { SRID = 4326 });
                if(newDataset.DataPoints.Any(x=> x.Location == loc))
                {
                    Console.WriteLine("Location was the same :) ");

                    var existing = newDataset.DataPoints.FirstOrDefault(x => x.Location == loc);
                    if (existing.Timestamp > point.Timestamp)
                        existing.Timestamp = point.Timestamp;
                    if (existing.TimestampExit < point.Timestamp)
                        existing.TimestampExit = point.Timestamp;


                }
                else
                {
                    Console.WriteLine("New location found");
                    var dp = new DataPoint()
                    {
                        Location = loc,
                        Accuracy = point.Accuracy,
                        Altitude = point.Altitude,
                        User = User,
                        Timestamp = point.Timestamp,
                        TimestampExit = point.Timestamp.AddSeconds(1),
                        VerticalAccuracy = point.VerticalAccuracy,
                        Activities = new List<DataActivity>()
                    };
                    newDataset.DataPoints.Add(dp);
                }
            }

            ctx.DataSet.Update(newDataset);
            ctx.SaveChanges();

            return Ok(JsonConvert.SerializeObject(obj));

            //Console.WriteLine("Filtered entries: " + locations.Count());

            //foreach (var point in locations)
            //{
            //    var dp = new DataPoint()
            //    {
            //        Location = new NetTopologySuite.Geometries.Point(point.LongitudeE7 / 10000000, point.LatitudeE7 / 10000000) { SRID = 4326 },
            //        Accuracy = point.Accuracy,
            //        Altitude = point.Altitude,
            //        User = User,
            //        Timestamp = point.Timestamp,
            //        VerticalAccuracy = point.VerticalAccuracy,
            //        Activities = new List<DataActivity>()
            //    };

            //    if (point.Activities != null)
            //    {
            //        foreach (var ac in point.Activities)
            //        {
            //            if (ac.ActivityItems != null)
            //            {
            //                dp.Activities.Add(new DataActivity()
            //                {
            //                    ActivityPoints = ac.ActivityItems.Select(x => new DataActivityPoint() { Confidence = x.Confidence, Type = x.Type }).ToList(),
            //                    Timestamp = new DateTime(1970, 01, 01).AddMilliseconds(long.Parse(ac.TimestampMS))
            //                });
            //            }
            //        }
            //    }
            //    Console.WriteLine("Finished prepping object: " + newDataset.DataPoints.Count);
            //    newDataset.DataPoints.Add(dp);
            //}

        }

        private Point GeneralizePoint(Point p)
        {
            p.X = Math.Round(p.X * 40000) / 40000;
            p.Y = Math.Round(p.Y * 40000) / 40000;
            return p;
        }

        public async Task<string> ReadAsStringAsync(IFormFile file)
        {
            var result = new StringBuilder();
            using (var reader = new StreamReader(file.OpenReadStream()))
            {
                while (reader.Peek() >= 0)
                    result.AppendLine(await reader.ReadLineAsync());
            }
            return result.ToString();
        }
    }
}