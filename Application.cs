using System;
using System.IO;
using System.Reflection;
using System.Threading.Tasks;


namespace HangulDetector
{
	/// <summary>
	/// 애플리케이션.
	/// </summary>
	public class Application
	{
		/// <summary>
		/// 제외 문자열 목록.
		/// </summary>
		private static string[] s_ExcludeWords = new string[]
		{
			"//",
			"/*",
			"*/",
			"Debug.",
			"PrintLog(",
			"PrintError(",
			"PrintLogError(",
			"Obsolete(",
			"UIManager.Instance.EditorShowSystemMessage",
			"UnityEditor",
		};

		/// <summary>
		/// 디버깅을 위한 기본 경로.
		/// </summary>
		private static string s_DefaultPathByDebugOnly = "D:\\Github\\IdleGame\\Assets";

		/// <summary>
		/// 애플리케이션 진입점.
		/// </summary>
		public static void Main(string[] arguments)
		{
			// 경로가 없을 때.
			if (arguments.Length == 0)
			{
#if DEBUG
				// 디버깅용 경로.
				arguments = new string[] { s_DefaultPathByDebugOnly };
#else
				// 현재 실행 파일 경로.
				var assembly = Assembly.GetExecutingAssembly();
				var executeFileDirectory = Path.GetDirectoryName(assembly.Location).Replace(HangulDetector.BackSlash, HangulDetector.Slash);
				arguments = new string[] { executeFileDirectory };
#endif
			}

			// 실행.
			var rootPath = arguments[0];
			var detector = new HangulDetector(rootPath, s_ExcludeWords);
			var task = detector.DetectAsync();
			Task.WaitAll(task);

			Console.ReadLine();
		}
	}
}