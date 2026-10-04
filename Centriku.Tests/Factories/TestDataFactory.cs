using System.Collections.Generic;
using Centriku.Models;

namespace Centriku.Tests.Factories
{
    public static class TestDataFactory
    {
        // 1. GENERATE A SINGLE STUDENT
        public static Student CreateFakeStudent(string studentId = "2024-0001", string firstName = "John", string lastName = "Doe")
        {
            return new Student
            {
                StudentID = studentId,
                FirstName = firstName,
                LastName = lastName,
                Program = "BSIT",
                GradeYearLevel = "3rd Year",
                SectionName = "A",
                EnrollmentStatus = "Enrolled",
                IsArchived = false
            };
        }

        // 2. GENERATE A LIST OF STUDENTS
        public static List<Student> CreateFakeStudentList(int count)
        {
            var students = new List<Student>();
            for (int i = 1; i <= count; i++)
            {
                students.Add(CreateFakeStudent(
                    studentId: $"2024-{i:D4}", 
                    firstName: $"Student{i}", 
                    lastName: $"Test{i}"
                ));
            }
            return students;
        }

        // 3. GENERATE A FAKE ASSESSMENT (QUIZ)
        public static Assessment CreateFakeAssessment(int id = 1, string title = "Quiz 1", double maxScore = 100)
        {
            return new Assessment
            {
                AssessmentID = id,
                ClassID = 101,
                Title = title,
                Category = "Quizzes",
                GradingPeriod = "Midterm",
                AssessmentType = "Solo",
                MaxScore = maxScore
            };
        }
    }
}