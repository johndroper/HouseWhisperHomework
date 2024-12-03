using Amazon.BedrockRuntime;
using Amazon.BedrockRuntime.Model;
using Amazon.Runtime;
using Amazon.Runtime.Internal;
using Ical.Net;
using Ical.Net.CalendarComponents;
using Ical.Net.DataTypes;
using Ical.Net.Serialization;
using Microsoft.AspNetCore.Mvc;
using Newtonsoft.Json;
using System;
using System.Globalization;
using Calendar = Ical.Net.Calendar;

namespace HouseWhisperHomework
{
  public class TimeSlot
  {
    public DateTime? Start { get; set; }
    public DateTime? End { get; set; }
  }

  public static class CalendarHelper
  {
    public static DateTime[] GetLeastBusyDays(Calendar calendar, DateTime start, DateTime end)
    {
      Dictionary<DateTime, int> eventCountByDay = [];

      for (var date = start.Date; date <= end.Date; date = date.AddDays(1))
      {
        eventCountByDay[date] = 0;
      }

      foreach (var calendarEvent in calendar.Events)
      {
        var eventStart = calendarEvent.Start.AsDateTimeOffset.Date;
        if (eventCountByDay.ContainsKey(eventStart.Date))
          eventCountByDay[eventStart.Date]++;
      }

      return eventCountByDay
        .OrderBy(pair => pair.Value)
        .Select(pair => pair.Key).ToArray();
    }

    public static TimeSlot[] GetFreeTimeSlots(Calendar calendar, DateTime startDate, DateTime endDate, float durationHours, int numberOfSlots)
    {
      var leastBusyDays = GetLeastBusyDays(calendar, startDate, endDate);
      var freeTimeSlots = new List<TimeSlot>();
      foreach (var day in leastBusyDays)
      {
        var start = day.Date.AddHours(8);
        var end = day.Date.AddHours(18);
        for (var current = start; current < end; current = current.AddHours(durationHours))
        {
          if (IsTimeFree(calendar, current, durationHours))
          {
            freeTimeSlots.Add(new TimeSlot
            {
              Start = current,
              End = current.AddHours(durationHours)
            });
          }
          if (freeTimeSlots.Count >= numberOfSlots)
            break;
        }
      }
      return freeTimeSlots.ToArray();
    }

    public static bool IsTimeFree(Calendar calendar, DateTime startDateTime, float durationHours)
    {
      try
      {
        var startDateTimeUtc = new CalDateTime(startDateTime.ToUniversalTime());
        var endDateTimeUtc = new CalDateTime(startDateTime.AddHours(durationHours).ToUniversalTime());
        foreach (var calendarEvent in calendar.Events)
        {
          var eventStart = calendarEvent.Start.AsUtc;
          var eventEnd = calendarEvent.End.AsUtc;
          // Check if any overlap exists
          if ((startDateTimeUtc.AsUtc >= eventStart && startDateTimeUtc.AsUtc < eventEnd) || // Overlaps the start
              (endDateTimeUtc.AsUtc > eventStart && endDateTimeUtc.AsUtc <= eventEnd) ||    // Overlaps the end
              (startDateTimeUtc.AsUtc <= eventStart && endDateTimeUtc.AsUtc >= eventEnd))   // Encloses the event
            return false;
        }
        return true; 
      }
      catch (Exception ex)
      {
        throw new Exception("Error finding free time.", ex)
          .AddData("startDateTime", startDateTime)
          .AddData("durationHours", durationHours);
      }
    }

    private static Random random = new Random();

    private class DummyEvent
    {
      public string? EventName { get; set; }
      public string? Description { get; set; }
      public string? Location { get; set; }
    }

    private static string[] actors = new string[]
    {
        "Chris Evans", "Emma Stone", "Leonardo DiCaprio", "Scarlett Johansson", "Ryan Reynolds",
        "Anne Hathaway", "Brad Pitt", "Jennifer Lawrence", "Denzel Washington", "Margot Robbie",
        "Tom Cruise", "Zendaya", "Robert Downey Jr", "Cate Blanchett", "Tom Hanks",
        "Natalie Portman", "Christian Bale", "Charlize Theron", "Keanu Reeves", "Emily Blunt",
        "Hugh Jackman", "Gal Gadot", "Joaquin Phoenix", "Nicole Kidman", "Matt Damon",
        "Sandra Bullock", "Benedict Cumberbatch", "Zoe Saldana", "Viola Davis", "Jason Momoa",
        "Brie Larson", "Jake Gyllenhaal", "Jessica Chastain", "Idris Elba", "Angelina Jolie",
        "Michael B Jordan", "Amy Adams", "Chris Hemsworth", "Florence Pugh", "Timothée Chalamet",
        "Kristen Stewart", "Daniel Day-Lewis", "Julia Roberts", "Andrew Garfield", "Salma Hayek",
        "Will Smith", "Michelle Yeoh", "Adam Driver", "Rachel McAdams", "Mark Ruffalo", "Halle Berry"
    };

    public static async Task<CalendarEvent> GenerateRandomCalendarEventAsync(
      Calendar calendar,
      DateTime startDate,
      DateTime endDate,
      int maxDurationHours,
      AwsHelper awsHelper)
    {
      try
      {
        var actor = actors[random.Next(actors.Length)];
        var prompt = $@"
          A Hollywood star { actor } is moving to Seattle.
          Create a realistic calendar event for a real estate agent with a:
          - Name (e.g., meeting with { actor } about some common real estate activity.)
          - Description (e.g., some very short comments (less than 50 words) about what about this property is perfect for {actor}.)
          - Location (e.g., a fictional address of the property where this is taking place. Should be in the Seattle area.)
          Return JSON format like:
          {{
              ""EventName"": ""<name>"",
              ""Description"": ""<description>"",
              ""Location"": ""<location>""
          }}
          It is important that you respond with only JSON and nothing else.";

        DummyEvent? dummyEvent = null;
        int tries = 0;
        while (dummyEvent == null)
        {
          string response = await awsHelper.GetFromAws(prompt);
          if (string.IsNullOrWhiteSpace(response))
            throw new Exception("AI response is empty.");
          try
          {
            dummyEvent = JsonConvert.DeserializeObject<DummyEvent>(response);
          }
          catch (Exception ex)
          {
            Console.WriteLine("");
            Console.WriteLine($"DummyEvent failed to deserialize: {ex.Message}");
            Console.WriteLine(response);
            tries++;
            if (tries > 10)
              throw new Exception("DummyEvent failed to deserialize.", ex);
          }
        }
        var range = (endDate - startDate).TotalDays;
        var i = 0;
        DateTime randomDate;
        double randomDuration;
        do
        {
          randomDate = startDate
          .AddDays(random.NextDouble() * range)
          .Date
          .AddHours(random.NextFractionalFloat(8, 18, 4));
          randomDuration = random.NextFractionalFloat(1, maxDurationHours, 4);
          i++;
          if (i > 1000)
            throw new Exception("Too many attempts to generate a random event.");
        }
        while (!IsTimeFree(calendar, randomDate, maxDurationHours));

        return new CalendarEvent
        {
          Summary = dummyEvent.EventName,
          Start = new CalDateTime(randomDate),
          End = new CalDateTime(randomDate.AddHours(randomDuration)),
          Description = dummyEvent.Description,
          Location = dummyEvent.Location,
          IsAllDay = false
        };
      }
      catch (Exception ex)
      {
        Console.WriteLine($"Error generating event: {ex.Message}");
        throw;
      }
    }
  }
}
