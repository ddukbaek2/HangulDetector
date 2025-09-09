using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading.Tasks;


namespace HangulDetector
{
	/// <summary>
	/// 지정한 디렉토리 안의 C# 소스 파일을 훑어 한글 문자열을 자동으로 검출하여 기록하는 클래스.
	/// </summary>
	public class HangulDetector
	{
		/// <summary>
		/// 쌍따옴표 문자.
		/// </summary>
		public const char DoubleQuote = '\"';

		/// <summary>
		/// 슬래시 문자.
		/// </summary>
		public const char Slash = '/';

		/// <summary>
		/// 역슬래시 문자.
		/// </summary>
		public const char BackSlash = '\\';

		/// <summary>
		/// 캐리지 리턴 문자.
		/// </summary>
		public const char CarriageReturn = '\r';

		/// <summary>
		/// 라인 피드 문자.
		/// </summary>
		public const char LineFeed = '\n';

		/// <summary>
		/// 파일 검색 패턴.
		/// </summary>
		public const string FileSearchPattern = "*.cs";

		/// <summary>
		/// 정규표현식 한글 검색 패턴.
		/// </summary>
		public const string HangulSearchPattern = "[ㄱ-ㅎ가-힣]";

		/// <summary>
		/// 파일스탬프 출력 포맷.
		/// </summary>
		public const string TimestampFormat = "yyyy-MM-dd_HHmmss";

		/// <summary>
		/// 루트 디렉토리.
		/// </summary>
		private string m_RootPath;

		/// <summary>
		/// 제외 문자열 목록.
		/// </summary>
		private string[] m_ExcludeWords;

		/// <summary>
		/// 처리 결과.
		/// </summary>
		private ConcurrentDictionary<string, ConcurrentBag<Tuple<int, string>>> m_DetectedTexts;
	
		/// <summary>
		/// 생성됨.
		/// </summary>
		public HangulDetector(string rootPath, string[] excludeWords)
		{
			m_RootPath = rootPath;
			m_ExcludeWords = excludeWords;
			m_DetectedTexts = new ConcurrentDictionary<string, ConcurrentBag<Tuple<int, string>>>();
		}

		/// <summary>
		/// 비동기 처리.
		/// </summary>
		public async Task DetectAsync()
		{
			if (!Directory.Exists(m_RootPath))
				throw new DirectoryNotFoundException(m_RootPath);

			Console.WriteLine("[HangulDetector] Start.");

			// 파일 목록 생성.
			var enumeable = Directory.EnumerateFiles(m_RootPath, HangulDetector.FileSearchPattern, SearchOption.AllDirectories);
			enumeable = enumeable.Select(filePath => filePath.Replace(HangulDetector.BackSlash, HangulDetector.Slash));
			var filePaths = new List<string>(enumeable);

			Console.WriteLine($"[HangulDetector] Find Files: {filePaths.Count}");

			// 비우기.
			m_DetectedTexts.Clear();

			// 비동기 실행.
			var tasks = new List<Task>();
			foreach (var filePath in filePaths)
			{
				var task = ReadFileInHangulTextAsync(filePath);
				tasks.Add(task);
			}

			// 전체 작업이 완료될 때까지 대기.
			await Task.WhenAll(tasks);

			// 결과 파일 저장.
			// 저장 위치는 비워둘 경우 프로그램 실행 파일 위치로 저장.
			//CreateReportToFile(m_RootPath);
			CreateReportToFile(string.Empty);

			Console.WriteLine("[HangulDetector] Complete.");
		}

		/// <summary>
		/// 제외 문자열 포함 여부 확인.
		/// </summary>
		private bool FindExcludeWords(string text)
		{
			foreach (var excludeWord in m_ExcludeWords)
			{
				if (text.Contains(excludeWord))
					return true;
			}

			return false;
		}

		/// <summary>
		/// 파일안의 텍스트를 비동기로 읽어서 검출.
		/// </summary>
		private async Task ReadFileInHangulTextAsync(string filePath)
		{
			var texts = await File.ReadAllTextAsync(filePath);
			var fileName = Path.GetFileName(filePath);

			var lineNumber = 1;
			foreach (var text in texts.Split(HangulDetector.LineFeed))
			{
				var line = text.TrimEnd(HangulDetector.CarriageReturn).Trim();

				// 제외문자열이 들어있다면 제외.
				if (FindExcludeWords(line))
				{
					++lineNumber;
					continue;
				}

				// 쌍따옴표가 없는 글은 문자열이 아님.
				if (!line.Contains(HangulDetector.DoubleQuote))
				{
					++lineNumber;
					continue;
				}

				// 한글이 없다면 상관없음.
				if (!Regex.IsMatch(line, HangulDetector.HangulSearchPattern))
				{
					++lineNumber;
					continue;
				}

				// 내용 저장.
				if (!m_DetectedTexts.TryGetValue(filePath, out var detectedTexts))
				{
					detectedTexts = new ConcurrentBag<Tuple<int, string>>();
					m_DetectedTexts[filePath] = detectedTexts;
				}
				var detectedText = new Tuple<int, string>(lineNumber, line);
				detectedTexts.Add(detectedText);

				// 콘솔 출력.
				var result = $" [HangulDetector] {fileName} ({lineNumber}): {line}";
				Console.WriteLine(result);

				++lineNumber;
			}
		}

		/// <summary>
		/// 리포트 파일 저장.
		/// </summary>
		private void CreateReportToFile(string reportFileDirectory, bool sort = true)
		{
			// 저장 경로가 없을 경우 실행 파일 위치로 생성.
			if (!Directory.Exists(reportFileDirectory))
			{
				var assembly = Assembly.GetExecutingAssembly();
				reportFileDirectory = Path.GetDirectoryName(assembly.Location).Replace(HangulDetector.BackSlash, HangulDetector.Slash);
			}

			// 데이터 생성.
			var stringBuilder = new StringBuilder();

			// 파일경로 정렬.
			var sortedKeys = new List<string>(m_DetectedTexts.Keys);
			if (sort)
				sortedKeys.Sort(StringComparer.Ordinal);

			foreach (var filePath in sortedKeys)
			{
				// 줄번호 정렬.
				var detectedTexts = m_DetectedTexts[filePath].ToList();
				if (sort)
					detectedTexts.Sort((left, right) => left.Item1.CompareTo(right.Item1));

				foreach (var detectedText in detectedTexts)
				{
					var lineNumber = detectedText.Item1;
					var lineText = detectedText.Item2;
					stringBuilder.AppendLine($"{filePath} ({lineNumber}): {lineText}");
				}
			}

			// 파일 저장.
			var timestamp = DateTime.Now.ToString(HangulDetector.TimestampFormat);
			var content = stringBuilder.ToString();
			var reportFilePath = $"{reportFileDirectory}/HangulDetector_{timestamp}.txt";
			File.WriteAllText(reportFilePath, content);
			Console.WriteLine($"[HangulDetector] Report created: {reportFilePath}");
		}
	}
}