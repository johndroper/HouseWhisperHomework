using Microsoft.AspNetCore.Mvc;
using System.Globalization;
using Ical.Net;
using Ical.Net.CalendarComponents;
using Ical.Net.DataTypes;
using Ical.Net.Serialization;
using System;
using System.Net.Http;
using System.Threading.Tasks;
using Calendar = Ical.Net.Calendar;
using Amazon.BedrockRuntime.Model;
using HouseWhisperHomework.Models;


namespace HouseWhisperHomework.Controllers
{
  public class HomeController(IConfiguration Configuration) : Controller
  {
    public string IcsUrl1
    {
      get
      {
        var icsUrl = Configuration["IcsUrl"];
        if (string.IsNullOrEmpty(icsUrl))
          throw new Exception("IcsUrl is not set in the configuration");
        return icsUrl;
      }
    }

    public string IcsUrl2
    {
      get
      {
        var icsUrl = Configuration["IcsUrl2"];
        if (string.IsNullOrEmpty(icsUrl))
          throw new Exception("IcsUrl2 is not set in the configuration");
        return icsUrl;
      }
    }

    public async Task<Calendar> GetCalendar(string icsUrl)
    {
      using (HttpClient client = new HttpClient())
      {
        string icsContent = await client.GetStringAsync(icsUrl);
        return Calendar.Load(icsContent);
      }
    }

    public AwsHelper AwsHelper
    {
      get
      {
        return new AwsHelper(Configuration);
      }
    }

    [HttpPost]
    public async Task<FileResult> CreateTestCalendar(int numberOfEvents, DateTime rangeStartDate, DateTime rangeEndDate, int maxDurationHours)
    {
      if (rangeEndDate <= rangeStartDate)
        throw new Exception("rangeEndDate must be greater than rangeStartDate")
          .AddData("rangeStartDate", rangeStartDate)
          .AddData("rangeEndDate", rangeEndDate);
      try
      {
        Calendar calendar = await GetCalendar(IcsUrl1);
        for (int i = 0; i < numberOfEvents; i++)
        {
          calendar.Events.Add(await CalendarHelper.GenerateRandomCalendarEventAsync(
            calendar,
            rangeStartDate,
            rangeEndDate,
            maxDurationHours,
            AwsHelper));
        }
        var serializer = new CalendarSerializer();
        var responseStream = new MemoryStream();
        serializer.Serialize(calendar, responseStream, System.Text.Encoding.UTF8);
        responseStream.Seek(0, SeekOrigin.Begin);
        return File(responseStream, "text/calendar", $"test calendar with {numberOfEvents} events from {rangeStartDate.ToString("yyyy-MM-dd")} to {rangeEndDate.ToString("yyyy-MM-dd")}.ical");
      }
      catch (Exception ex)
      {
        Exception exception = new Exception("Error creating test calendar", ex);
        exception.Data.Add("numberOfEvents", numberOfEvents);
        exception.Data.Add("rangeStartDate", rangeStartDate);
        exception.Data.Add("rangeEndDate", rangeEndDate);
        throw exception;
      }
    }

    public async Task<IActionResult> Index()
    {
      var calendar = await GetCalendar(IcsUrl1);
      return View(new IndexData(calendar.Events.Count));
    }

    public IActionResult GetTestData()
    {
      return View();
    }

    public IActionResult AmIFree()
    {
      return View();
    }

    public class AvailabilityCheckRequest
    {
      public string? DateTime { get; set; }
      public float DurationHours { get; set; }
    }

    [HttpPost]
    public async Task<IActionResult> CheckAvailability([FromBody] AvailabilityCheckRequest request)
    {
      try
      {
        if (request.DateTime == null)
          throw new Exception("DateTime is required");
        Calendar calendar = await GetCalendar(IcsUrl1);
        var dateTimeToCheck = DateTime.Parse(request.DateTime, CultureInfo.InvariantCulture);
        bool isFree = CalendarHelper.IsTimeFree(calendar, dateTimeToCheck, request.DurationHours);
        return Json(new { isFree });
      }
      catch (Exception ex)
      {
        throw new Exception("Error checking availability", ex)
          .AddData("dateTime", request.DateTime)
          .AddData("durationHours", request.DurationHours);
      }
    }
    public class FreeTimeSlotsRequest
    {
      public string? StartDate { get; set; }
      public string? EndDate { get; set; }
      public float DurationHours { get; set; }
    }

    public class GetFreeTimeSlotsRequest
    {
      public FreeTimeSlotsRequest[] FreeTileSlotRequests { get; set; } = [];
      public int NumberOfSlots { get; set; }
    }

    private static TimeSlot[] FindFreeTimeSlots(GetFreeTimeSlotsRequest request, Calendar calendar)
    {
      List<TimeSlot> freeTimeSlots = [];
      foreach (var freeSlotRequest in request.FreeTileSlotRequests)
      {
        if (freeSlotRequest.StartDate == null)
          throw new Exception("StartDate is required");
        if (freeSlotRequest.EndDate == null)
          throw new Exception("EndDate is required");
        DateTime startDate = DateTime.Parse(freeSlotRequest.StartDate, CultureInfo.InvariantCulture);
        DateTime endDate = DateTime.Parse(freeSlotRequest.EndDate, CultureInfo.InvariantCulture);
        var timeSlots = CalendarHelper.GetFreeTimeSlots(calendar, startDate, endDate, freeSlotRequest.DurationHours, request.NumberOfSlots);
        freeTimeSlots.AddRange(timeSlots.Take(request.NumberOfSlots));
        if (freeTimeSlots.Count >= request.NumberOfSlots)
          break;
      }
      return freeTimeSlots.ToArray();
    }

    public async Task<IActionResult> GetFreeTimeSlots([FromBody] GetFreeTimeSlotsRequest request)
    {
      if (!request.FreeTileSlotRequests.Any())
        throw new Exception("At least one FreeTimeSlotsRequest is required");
      if (request.NumberOfSlots <= 0)
        throw new Exception("NumberOfSlots must be greater than 0");
      try
      {
        Calendar calendar = await GetCalendar(IcsUrl1);
        var freeTimeSlots = FindFreeTimeSlots(request, calendar);
        return Ok(freeTimeSlots);
      }
      catch (Exception ex)
      {
        throw new Exception("Error getting free time slots", ex);
      }
    }

    public IActionResult FreeTimeSlots()
    {
      return View();
    }

    public class GetFreeTimeSlotsMultiAgentRequest : GetFreeTimeSlotsRequest
    {
      public string[] AgentUrls { get; set; } = [];
    }

    public async Task<IActionResult> GetFreeTimeSlotsMultiAgent([FromBody] GetFreeTimeSlotsMultiAgentRequest request)
    {
      if (!request.FreeTileSlotRequests.Any())
        throw new Exception("At least one FreeTimeSlotsRequest is required");
      if (request.NumberOfSlots <= 0)
        throw new Exception("NumberOfSlots must be greater than 0");
      try
      {
        Calendar calendar = await GetCalendar(request.AgentUrls[0]);
        for(int i = 1; i < request.AgentUrls.Length; i++)
        {
          Calendar calendar2 = await GetCalendar(request.AgentUrls[i]);
          calendar.MergeWith(calendar2);
        }
        var freeTimeSlots = FindFreeTimeSlots(request, calendar);
        return Ok(freeTimeSlots);
      }
      catch (Exception ex)
      {
        throw new Exception("Error getting free time slots", ex);
      }
    }

    public IActionResult FreeTimeSlotsMultiAgent()
    {
      return View(new FreeTimeSlotsMultiAgentData(IcsUrl1,IcsUrl2));
    }

    public async Task<IActionResult> GetDaySlot(string date, float freeTimeThresholdHours)
    {
      try
      {
        if (string.IsNullOrEmpty(date))
          throw new Exception("Date is required");
        DateTime selectedDate = DateTime.Parse(date, CultureInfo.InvariantCulture);
        Calendar calendar = await GetCalendar(IcsUrl1);
        var freeTimeSlots = CalendarHelper.GetFreeTimeSlots(calendar, selectedDate, selectedDate, freeTimeThresholdHours, 1);
        if (freeTimeSlots.Length > 0)
          return Ok(
            new
            {
              free = true,
              freeTimeSlot = freeTimeSlots[0]
            });
        else
          return Ok(new {
              free = false
            });
      }
      catch (Exception ex)
      {
        throw new Exception("Error checking if day is free", ex)
            .AddData("date", date)
            .AddData("freeTimeThresholdHours", freeTimeThresholdHours);
      }
    }

    public IActionResult IsDayFree()
    {
      return View();
    }
  }


}
