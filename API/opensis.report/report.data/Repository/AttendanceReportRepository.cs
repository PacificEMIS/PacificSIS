﻿using opensis.data.Interface;
using opensis.data.Models;
using opensis.report.report.data.Interface;
using opensis.report.report.data.ViewModels.AttendanceReport;
using System.Text.RegularExpressions;
using System.Linq;
using System;
using System.Collections.Generic;
using System.Text;
using System.Threading.Tasks;
using Microsoft.EntityFrameworkCore;
using opensis.data.ViewModels.CourseManager;

namespace opensis.report.report.data.Repository
{
    public class AttendanceReportRepository : IAttendanceReportRepository
    {
        private readonly CRMContext? context;
        private static readonly string NORECORDFOUND = "No Record Found";
        public AttendanceReportRepository(IDbContextFactory dbContextFactory)
        {
            this.context = dbContextFactory.Create();
        }


        /// <summary>
        /// Get Student Attendanced  Report
        /// </summary>
        /// <param name="pageResult"></param>
        /// <returns></returns>
        public StudentAttendanceReport GetStudentAttendanceReport(PageResult pageResult)
        {
            //IQueryable<StudentMaster>? transactionIQ = null;
            //IQueryable<StudentMaster>? studentDataList = null;
            int? totalCount = 0;
            StudentAttendanceReport studentAttendanceReport = new();

            //var studentDataList = this.context?.StudentAttendance.Include(s => s.BlockPeriod).Include(s => s.AttendanceCodeNavigation).Include(s => s.StudentCoursesectionSchedule).ThenInclude(s => s.StudentMaster).ThenInclude(s => s.StudentEnrollment).Where(x => x.TenantId == pageResult.TenantId && x.SchoolId == pageResult.SchoolId && (pageResult.IncludeInactive == false || pageResult.IncludeInactive == null ? x.IsActive != false : true)).ToList();
            //studentAttendanceReport.studentMasters = studentDataList ?? null;
            return studentAttendanceReport;

        }

        /// <summary>
        /// GetAverageDailyAttendanceReport
        /// </summary>
        /// <param name="pageResult"></param>
        /// <returns></returns>
        public AverageDailyAttendanceViewModel GetAverageDailyAttendanceReport(PageResult pageResult)
        {
            AverageDailyAttendanceViewModel averageDailyAttendance = new();
            List<DateTime> holidayList = new List<DateTime>();
            try
            {
                averageDailyAttendance.FromDate = pageResult.MarkingPeriodStartDate;
                averageDailyAttendance.ToDate = pageResult.MarkingPeriodEndDate;
                averageDailyAttendance.TenantId = pageResult.TenantId;
                averageDailyAttendance.SchoolId = pageResult.SchoolId;
                averageDailyAttendance.AcademicYear = pageResult.AcademicYear;
                averageDailyAttendance._tenantName = pageResult._tenantName;
                averageDailyAttendance._token = pageResult._token;

                var studentAttendanceData = this.context?.StudentAttendance.Include(x => x.AttendanceCodeNavigation).Join(this.context?.AllCourseSectionView, sa => sa.CourseSectionId, acsv => acsv.CourseSectionId, (sa, acsv) => new { sa, acsv }).AsEnumerable().Where(x => x.sa.TenantId == pageResult.TenantId && x.sa.SchoolId == pageResult.SchoolId && x.sa.AttendanceDate >= pageResult.MarkingPeriodStartDate && x.sa.AttendanceDate <= pageResult.MarkingPeriodEndDate && x.acsv.TenantId == pageResult.TenantId && x.acsv.SchoolId == pageResult.SchoolId && (x.acsv.AttendanceTaken == true || x.acsv.TakeAttendanceCalendar == true || x.acsv.TakeAttendanceVariable == true || x.acsv.TakeAttendanceBlock == true));

                if (studentAttendanceData?.Any() == true)
                {
                    var calenderData = this.context?.SchoolCalendars.FirstOrDefault(x => x.TenantId == pageResult.TenantId && x.SchoolId == pageResult.SchoolId && x.AcademicYear == pageResult.AcademicYear && x.SessionCalendar == true);

                    var CalendarEventsData = this.context?.CalendarEvents.Where(e => e.TenantId == pageResult.TenantId && e.CalendarId == calenderData.CalenderId && e.IsHoliday == true && (e.SchoolId == pageResult.SchoolId || e.ApplicableToAllSchool == true)).ToList();

                    //fetch holidays
                    if (CalendarEventsData != null && CalendarEventsData.Any())
                    {
                        foreach (var calender in CalendarEventsData)
                        {
                            if (calender.EndDate!.Value.Date > calender.StartDate!.Value.Date)
                            {
                                var date = Enumerable.Range(0, 1 + (calender.EndDate.Value.Date - calender.StartDate.Value.Date).Days)
                                   .Select(i => calender.StartDate.Value.Date.AddDays(i))
                                   .ToList();
                                holidayList.AddRange(date);
                            }
                            holidayList.Add(calender.StartDate.Value.Date);
                        }
                    }

                    //fetch calender days & weekoff days
                    List<char> daysValue = new List<char> { '0', '1', '2', '3', '4', '5', '6' };
                    var calenderDays = calenderData.Days.ToCharArray();
                    var WeekOffDays = daysValue.Except(calenderDays);
                    var WeekOfflist = new List<string>();
                    foreach (var WeekOffDay in WeekOffDays)
                    {
                        Days days = new Days();
                        var Day = Enum.GetName(days.GetType(), Convert.ToInt32(WeekOffDay.ToString()));
                        WeekOfflist.Add(Day!);
                    }

                    //fetch all dates in this session calender
                    var allDates = Enumerable.Range(0, 1 + calenderData.EndDate.Value.Date.Subtract(calenderData.StartDate.Value.Date).Days).Select(d => calenderData.StartDate.Value.Date.AddDays(d)).ToList();

                    //remove holidays &weekoffdays
                    var wrokingDates = allDates.Where(s => !holidayList.Contains(s.Date) && !WeekOfflist.Contains(s.Date.DayOfWeek.ToString()) && (s.Date >= pageResult.MarkingPeriodStartDate && s.Date <= pageResult.MarkingPeriodEndDate)).ToList();

                    var gradeLevels = studentAttendanceData.Select(s => s.acsv.CourseGradeLevel).Distinct().ToList();

                    var allCourseSectionData = this.context?.AllCourseSectionView.Where(x => x.TenantId == pageResult.TenantId && x.SchoolId == pageResult.SchoolId && x.AcademicYear == pageResult.AcademicYear && (x.AttendanceTaken == true || x.TakeAttendanceCalendar == true || x.TakeAttendanceVariable == true || x.TakeAttendanceBlock == true)).ToList();

                    foreach (var gradeLevel in gradeLevels)
                    {
                        AverageDailyAttendanceReport averageDailyAttendanceReport = new AverageDailyAttendanceReport();                       
                        List<int> courseSectionIds = new List<int>();
                        int? present = 0;
                        int? absent = 0;
                        int? other = 0;
                        int? studentInfo = 0;
                        int? totalPeriods = 0;

                        var gradeLevelWiseAttendance = studentAttendanceData.Where(x => x.acsv.CourseGradeLevel == gradeLevel);

                        if (gradeLevelWiseAttendance.Any())
                        {
                            //var allCourseSectionData = gradeLevelWiseAttendance.Select(s => s.acsv).ToList();                        
                            //courseSectionIds = allCourseSectionData.Select(s => s.CourseSectionId).Distinct().ToList();

                            courseSectionIds = gradeLevelWiseAttendance.Select(s => s.acsv.CourseSectionId).Distinct().ToList();

                            foreach (var csId in courseSectionIds)
                            {
                                var courseSectionData = allCourseSectionData.Where(x => x.CourseSectionId == csId).ToList();
                                
                                if (courseSectionData.FirstOrDefault().ScheduleType == "Fixed Schedule (1)")
                                {
                                    List<DateTime> dateList = new List<DateTime>();
                                    string[]? meetingDays = { };

                                    DateTime start = (DateTime)courseSectionData.FirstOrDefault().DurationStartDate;
                                    DateTime end = (DateTime)courseSectionData.FirstOrDefault().DurationEndDate;

                                    meetingDays = courseSectionData.FirstOrDefault().FixedDays!.ToLower().Split("|");
                                    bool allDays = meetingDays == null || !meetingDays.Any();

                                    dateList = Enumerable.Range(0, 1 + end.Subtract(start).Days)
                                                          .Select(offset => start.AddDays(offset))
                                                          .Where(d => allDays || meetingDays!.Contains(d.DayOfWeek.ToString().ToLower()))
                                                          .ToList();

                                    if (dateList.Count > 0)
                                    {
                                        dateList = dateList.Where(s => dateList.Any(secL => s.Date >= pageResult.MarkingPeriodStartDate.Value.Date && s.Date <= pageResult.MarkingPeriodEndDate.Value.Date && !holidayList.Contains(s.Date))).ToList();
                                        totalPeriods += dateList.Count;
                                    }
                                }
                                else if (courseSectionData.FirstOrDefault().ScheduleType == "Variable Schedule (2)")
                                {
                                    List<DateTime> dateList = new List<DateTime>();
                                    string[]? meetingDays = { };

                                    DateTime start = (DateTime)courseSectionData.FirstOrDefault().DurationStartDate;
                                    DateTime end = (DateTime)courseSectionData.FirstOrDefault().DurationEndDate;

                                    meetingDays = courseSectionData.Select(s => s.VarDay.ToLower()).ToArray();

                                    bool allDays = meetingDays == null || !meetingDays.Any();

                                    dateList = Enumerable.Range(0, 1 + end.Subtract(start).Days)
                                                          .Select(offset => start.AddDays(offset))
                                                          .Where(d => allDays || meetingDays!.Contains(d.DayOfWeek.ToString().ToLower())).ToList();

                                    if (dateList.Count > 0)
                                    {
                                        dateList = dateList.Where(s => dateList.Any(secL => s.Date >= pageResult.MarkingPeriodStartDate.Value.Date && s.Date <= pageResult.MarkingPeriodEndDate.Value.Date && !holidayList.Contains(s.Date))).ToList();
                                        int varPeriods = 0;
                                        foreach (var date in dateList)
                                        {
                                            var courseVariableScheduleData = courseSectionData.Where(e => e.VarDay != null && e.VarDay.Contains(date.DayOfWeek.ToString())).ToList();
                                            if (courseVariableScheduleData.Any())
                                            {
                                                varPeriods += courseVariableScheduleData.Count;
                                            }
                                        }
                                        totalPeriods += varPeriods;
                                    }
                                }
                                else if (courseSectionData.FirstOrDefault().ScheduleType == "Calendar Schedule (3)")
                                {
                                    var calendarDateList = courseSectionData.Where(s => s.CalDate.Value.Date >= pageResult.MarkingPeriodStartDate.Value.Date && s.CalDate.Value.Date <= pageResult.MarkingPeriodEndDate.Value.Date && !holidayList.Contains(s.CalDate.Value.Date)).ToList();

                                    if (calendarDateList.Count > 0)
                                    {
                                        totalPeriods += calendarDateList.Count;
                                    }
                                }
                                else if (courseSectionData.FirstOrDefault().ScheduleType == "Block Schedule (4)")
                                {
                                    var blockIds = courseSectionData.Select(x => x.BlockId).Distinct().ToList();
                                    foreach (var blockId in blockIds)
                                    {
                                        var bellScheduleList = this.context?.BellSchedule.Where(v => v.SchoolId == pageResult.SchoolId && v.TenantId == pageResult.TenantId && v.BlockId == blockId && v.BellScheduleDate >= pageResult.MarkingPeriodStartDate && v.BellScheduleDate <= pageResult.MarkingPeriodStartDate && (!holidayList.Contains(v.BellScheduleDate))).ToList();
                                        if (bellScheduleList.Count > 0)
                                        {
                                            totalPeriods += bellScheduleList.Count;
                                        }
                                    }
                                }
                            }

                            studentInfo = gradeLevelWiseAttendance.Select(s => new { s.sa.SchoolId, s.sa.StudentId }).Distinct().ToList().Count;
                            present = gradeLevelWiseAttendance.Where(x => x.sa.AttendanceCodeNavigation.Title.ToLower() == "present").Count();
                            absent = gradeLevelWiseAttendance.Where(x => x.sa.AttendanceCodeNavigation.Title.ToLower() == "absent").Count();
                            other = gradeLevelWiseAttendance.Where(x => x.sa.AttendanceCodeNavigation.Title.ToLower() != "present" && x.sa.AttendanceCodeNavigation.Title.ToLower() != "absent").Count();
                        }
                        averageDailyAttendanceReport.GradeLevel = gradeLevel;
                        averageDailyAttendanceReport.Students = studentInfo;
                        averageDailyAttendanceReport.DaysPossible = wrokingDates.Count;
                        averageDailyAttendanceReport.AttendancePossible = studentInfo * courseSectionIds.Count * totalPeriods;

                        averageDailyAttendanceReport.Present = studentInfo * present;
                        averageDailyAttendanceReport.Absent = studentInfo * absent;
                        averageDailyAttendanceReport.Other = studentInfo * other;
                        averageDailyAttendanceReport.NotTaken = averageDailyAttendanceReport.AttendancePossible - (averageDailyAttendanceReport.Present +
                        averageDailyAttendanceReport.Absent +
                        averageDailyAttendanceReport.Other);

                        averageDailyAttendanceReport.ADA = Math.Round((Convert.ToDecimal(averageDailyAttendanceReport.Present) / Convert.ToDecimal(averageDailyAttendanceReport.AttendancePossible) * 100), 2);
                        averageDailyAttendanceReport.AvgAttendance = Math.Round(Convert.ToDecimal(averageDailyAttendanceReport.Present) / Convert.ToDecimal(averageDailyAttendanceReport.AttendancePossible), 2);
                        averageDailyAttendanceReport.AvgAbsent = Math.Round(Convert.ToDecimal(averageDailyAttendanceReport.Absent) / Convert.ToDecimal(averageDailyAttendanceReport.AttendancePossible), 2);

                        averageDailyAttendance.averageDailyAttendanceReport.Add(averageDailyAttendanceReport);
                    }

                    if (pageResult.FilterParams != null && pageResult.FilterParams.ElementAt(0).ColumnName == null && pageResult.FilterParams.Count == 1)
                    {
                        string searchValue = pageResult.FilterParams.ElementAt(0).FilterValue;
                        averageDailyAttendance.averageDailyAttendanceReport = averageDailyAttendance.averageDailyAttendanceReport.Where(x => x.GradeLevel.ToLower().Contains(searchValue.ToLower()) || x.Students.ToString() == searchValue || x.DaysPossible.ToString() == searchValue || x.Present.ToString() == searchValue || x.Absent.ToString() == searchValue || x.Other.ToString() == searchValue || x.ADA.ToString() == searchValue || x.AvgAttendance.ToString() == searchValue || x.AvgAbsent.ToString() == searchValue).ToList();
                    }
                }
                else
                {
                    averageDailyAttendance._failure = true;
                    averageDailyAttendance._message = NORECORDFOUND;
                }
            }
            catch (Exception es)
            {
                averageDailyAttendance._failure = true;
                averageDailyAttendance._message = es.Message;
            }
            return averageDailyAttendance;
        }

        /// <summary>
        /// GetAverageAttendancebyDayReport
        /// </summary>
        /// <param name="pageResult"></param>
        /// <returns></returns>
        public AverageDailyAttendanceViewModel GetAverageAttendancebyDayReport(PageResult pageResult)
        {
            AverageDailyAttendanceViewModel averageDailyAttendance = new();
            List<DateTime> holidayList = new List<DateTime>();
            try
            {
                averageDailyAttendance.FromDate = pageResult.MarkingPeriodStartDate;
                averageDailyAttendance.ToDate = pageResult.MarkingPeriodEndDate;
                averageDailyAttendance.TenantId = pageResult.TenantId;
                averageDailyAttendance.SchoolId = pageResult.SchoolId;
                averageDailyAttendance.AcademicYear = pageResult.AcademicYear;
                averageDailyAttendance._tenantName = pageResult._tenantName;
                averageDailyAttendance._token = pageResult._token;

                var studentAttendanceData = this.context?.StudentAttendance.Include(x => x.AttendanceCodeNavigation).Join(this.context?.AllCourseSectionView, sa => sa.CourseSectionId, acsv => acsv.CourseSectionId, (sa, acsv) => new { sa, acsv }).AsEnumerable().Where(x => x.sa.TenantId == pageResult.TenantId && x.sa.SchoolId == pageResult.SchoolId && x.sa.AttendanceDate >= pageResult.MarkingPeriodStartDate && x.sa.AttendanceDate <= pageResult.MarkingPeriodEndDate && x.acsv.TenantId == pageResult.TenantId && x.acsv.SchoolId == pageResult.SchoolId && (x.acsv.AttendanceTaken == true || x.acsv.TakeAttendanceCalendar == true || x.acsv.TakeAttendanceVariable == true || x.acsv.TakeAttendanceBlock == true));

                if (studentAttendanceData?.Any() == true)
                {  
                    var attendanceDates = studentAttendanceData.Select(s => s.sa.AttendanceDate).Distinct().ToList();
                    foreach (var attendanceDate in attendanceDates)
                    {
                        AverageDailyAttendanceReport averageDailyAttendanceReport = new AverageDailyAttendanceReport();

                        var dateWiseAttendance = studentAttendanceData.Where(x => x.sa.AttendanceDate == attendanceDate).ToList();
                        if (dateWiseAttendance.Any())
                        {
                            var gradeLevels = dateWiseAttendance.Select(s => s.acsv.CourseGradeLevel).Distinct().ToList();
                            foreach (var gradeLevel in gradeLevels)
                            {
                                int? present = 0;
                                int? absent = 0;
                                int? other = 0;
                                int? studentInfo = 0;
                                var gradeLevelWiseAttendance = dateWiseAttendance.Where(x => x.acsv.CourseGradeLevel == gradeLevel);
                                if (gradeLevelWiseAttendance.Any())
                                {
                                    studentInfo = gradeLevelWiseAttendance.Select(s => new { s.sa.SchoolId, s.sa.StudentId }).Distinct().ToList().Count;
                                    present = gradeLevelWiseAttendance.Where(x => x.sa.AttendanceCodeNavigation.Title.ToLower() == "present").Count();
                                    absent = gradeLevelWiseAttendance.Where(x => x.sa.AttendanceCodeNavigation.Title.ToLower() == "absent").Count();
                                    other = gradeLevelWiseAttendance.Where(x => x.sa.AttendanceCodeNavigation.Title.ToLower() != "present" && x.sa.AttendanceCodeNavigation.Title.ToLower() != "absent").Count();
                                }

                                averageDailyAttendanceReport.Date = attendanceDate;
                                averageDailyAttendanceReport.GradeLevel = gradeLevel;
                                averageDailyAttendanceReport.Students = studentInfo;
                                averageDailyAttendanceReport.DaysPossible = 1;

                                averageDailyAttendanceReport.Present = present;
                                averageDailyAttendanceReport.Absent = absent;
                                averageDailyAttendanceReport.Other = other;
                               // averageDailyAttendanceReport.NotTaken =

                                //averageDailyAttendanceReport.ADA = 
                                averageDailyAttendanceReport.AvgAttendance = Math.Round(Convert.ToDecimal(averageDailyAttendanceReport.Present) / Convert.ToDecimal(averageDailyAttendanceReport.Students), 2);
                                averageDailyAttendanceReport.AvgAbsent = Math.Round(Convert.ToDecimal(averageDailyAttendanceReport.Absent) / Convert.ToDecimal(averageDailyAttendanceReport.Students), 2);

                                averageDailyAttendance.averageDailyAttendanceReport.Add(averageDailyAttendanceReport);
                            }
                        }
                    }

                    if (pageResult.FilterParams != null && pageResult.FilterParams.ElementAt(0).ColumnName == null && pageResult.FilterParams.Count == 1)
                    {
                        string searchValue = pageResult.FilterParams.ElementAt(0).FilterValue;
                        averageDailyAttendance.averageDailyAttendanceReport = averageDailyAttendance.averageDailyAttendanceReport.Where(x => x.GradeLevel.ToLower().Contains(searchValue.ToLower()) || x.Students.ToString() == searchValue || x.DaysPossible.ToString() == searchValue || x.Present.ToString() == searchValue || x.Absent.ToString() == searchValue || x.Other.ToString() == searchValue || x.ADA.ToString() == searchValue || x.AvgAttendance.ToString() == searchValue || x.AvgAbsent.ToString() == searchValue).ToList();
                    }

                    int totalCount = averageDailyAttendance.averageDailyAttendanceReport.Count;
                    if (pageResult.PageNumber > 0 && pageResult.PageSize > 0)
                    {
                        averageDailyAttendance.averageDailyAttendanceReport.Skip((pageResult.PageNumber - 1) * pageResult.PageSize).Take(pageResult.PageSize);
                    }
                    averageDailyAttendance.TotalCount = totalCount;
                    averageDailyAttendance.PageNumber = pageResult.PageNumber;
                    averageDailyAttendance._pageSize = pageResult.PageSize;
                }
                else
                {
                    averageDailyAttendance._failure = true;
                    averageDailyAttendance._message = NORECORDFOUND;
                }
            }
            catch (Exception es)
            {
                averageDailyAttendance._failure = true;
                averageDailyAttendance._message = es.Message;
            }
            return averageDailyAttendance;
        }
    }
}