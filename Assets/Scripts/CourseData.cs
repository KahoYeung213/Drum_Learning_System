using System;
using System.Collections.Generic;

[Serializable]
public class CourseCatalogData
{
    public List<DrumCourseData> courses = new List<DrumCourseData>();
}

[Serializable]
public class DrumCourseData
{
    public string id;
    public string title;
    public string description;
    public string difficulty;
    public List<CourseModuleData> modules = new List<CourseModuleData>();
}

[Serializable]
public class CourseModuleData
{
    public string id;
    public string title;
    public string description;
    public List<CourseLessonData> lessons = new List<CourseLessonData>();
}

[Serializable]
public class CourseLessonData
{
    public string id;
    public string title;
    public string description;
    public string objective;
    public List<LessonVideoData> learningVideos = new List<LessonVideoData>();
    public List<CourseExerciseData> exercises = new List<CourseExerciseData>();
}

[Serializable]
public class LessonVideoData
{
    public string title;
    public string videoFilePath;
    public string videoUrl;
    public string youtubeUrl;
}

[Serializable]
public class CourseExerciseData
{
    public string id;
    public string title;
    public string description;
    public string beatmapTitle;
    public string beatmapJsonPath;
    public int requiredScore = 70;
}
