using System;
using NUnit.Framework;

namespace Dockside.Tests
{
    /// <summary>Reminder times always land between 11:00 and 20:00 local time, and never sooner than they were due.</summary>
    public class NotificationScheduleTests
    {
        private static readonly DateTime Day = new DateTime(2026, 11, 19);

        [TestCase(14.0, 24, 14.0, 1, TestName = "Afternoon stays put")]
        [TestCase(6.5, 24, 11.0, 1, TestName = "Early morning waits until 11:00")]
        [TestCase(22.0, 24, 19.0, 1, TestName = "Late evening comes back to 19:00")]
        [TestCase(20.5, 24, 19.0, 1, TestName = "Just after 20:00 comes back to 19:00")]
        [TestCase(23.5, 24, 11.0, 2, TestName = "Near midnight waits until 11:00 the day after")]
        public void FitsTheDaytimeWindow(double leftHour, int delayHours, double expectHour, int expectDayOffset)
        {
            var left = Day.AddHours(leftHour);
            var fire = Notifications.FitWindow(left, left.AddHours(delayHours));
            Assert.AreEqual(Day.AddDays(expectDayOffset).Date, fire.Date);
            Assert.AreEqual(Math.Floor(expectHour), fire.Hour);
            Assert.That(fire.Hour, Is.InRange(11, 19));
        }

        [Test]
        public void EveryReminderIsInTheWindowAndAtLeast20HoursAway()
        {
            for (int minutes = 0; minutes < 24 * 60; minutes += 7)
            {
                var left = Day.AddMinutes(minutes);
                foreach (int delay in new[] { 24, 72, 168 })
                {
                    var fire = Notifications.FitWindow(left, left.AddHours(delay));
                    Assert.That(fire.Hour, Is.InRange(11, 19), $"left {left:HH:mm}, +{delay}h -> {fire:ddd HH:mm}");
                    Assert.That((fire - left).TotalHours, Is.GreaterThanOrEqualTo(20), $"left {left:HH:mm}, +{delay}h -> {fire:ddd HH:mm}");
                }
            }
        }
    }
}
