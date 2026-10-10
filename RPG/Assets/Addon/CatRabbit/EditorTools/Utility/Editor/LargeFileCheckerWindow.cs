using System;
using System.Collections.Generic;
using System.IO;
using UnityEditor;
using UnityEngine;

namespace CR
{
	public class LargeFileCheckerWindow : EditorWindow
	{
		private const long DefaultThresholdMb = 100;
		private static readonly string[] IgnoreDirectoryNames =
		{
			".git",
			"Library",
			"Logs",
			"Obj",
			"Temp"
		};

		private long m_ThresholdMb = DefaultThresholdMb;
		private bool m_IgnoreGeneratedFolders = true;
		private Vector2 m_ScrollViewPosition = Vector2.zero;
		private List<LargeFileInfo> m_LargeFiles = new List<LargeFileInfo>();
		private string m_LastScanMessage = string.Empty;

		private struct LargeFileInfo
		{
			public string FullPath;
			public string RelativePath;
			public long FileSizeBytes;
		}

		[MenuItem("CatRabbit/Tools/Check Large Files (>100MB)")]
		private static void OpenPanel()
		{
			LargeFileCheckerWindow window = GetWindow<LargeFileCheckerWindow>();
			window.minSize = new Vector2(760, 420);
			window.titleContent = new GUIContent("Large File Checker");
			window.Show();
		}

		private void OnGUI()
		{
			EditorGUILayout.BeginVertical();
			EditorGUILayout.Space(6f);

			m_ThresholdMb = EditorGUILayout.LongField("Threshold (MB)", m_ThresholdMb);
			if (m_ThresholdMb < 1)
			{
				m_ThresholdMb = 1;
			}

			m_IgnoreGeneratedFolders = EditorGUILayout.Toggle("Ignore Generated Folders", m_IgnoreGeneratedFolders);

			if (GUILayout.Button("Scan Project Files", GUILayout.Height(28f)))
			{
				ScanProjectFiles();
			}

			EditorGUILayout.Space(8f);
			if (!string.IsNullOrEmpty(m_LastScanMessage))
			{
				EditorGUILayout.HelpBox(m_LastScanMessage, MessageType.Info);
			}

			EditorGUILayout.LabelField("Matched Files", m_LargeFiles.Count.ToString());
			m_ScrollViewPosition = EditorGUILayout.BeginScrollView(m_ScrollViewPosition);

			for (int i = 0; i < m_LargeFiles.Count; i++)
			{
				LargeFileInfo fileInfo = m_LargeFiles[i];

				EditorGUILayout.BeginHorizontal();
				if (GUILayout.Button("Select", GUILayout.Width(64f)))
				{
					SelectAsset(fileInfo.RelativePath);
				}

				EditorGUILayout.LabelField(FormatBytes(fileInfo.FileSizeBytes), GUILayout.Width(98f));
				EditorGUILayout.SelectableLabel(fileInfo.RelativePath, GUILayout.Height(EditorGUIUtility.singleLineHeight));
				EditorGUILayout.EndHorizontal();
			}

			EditorGUILayout.EndScrollView();
			EditorGUILayout.EndVertical();
		}

		private void ScanProjectFiles()
		{
			m_LargeFiles.Clear();

			string projectRootPath = Directory.GetParent(Application.dataPath).FullName;
			long thresholdBytes = m_ThresholdMb * 1024L * 1024L;
			int scannedFileCount = 0;

			foreach (string filePath in EnumerateAllFiles(projectRootPath))
			{
				scannedFileCount++;

				FileInfo fileInfo = new FileInfo(filePath);
				if (fileInfo.Length >= thresholdBytes)
				{
					LargeFileInfo largeFileInfo = new LargeFileInfo();
					largeFileInfo.FullPath = fileInfo.FullName;
					largeFileInfo.RelativePath = ToRelativePath(fileInfo.FullName, projectRootPath);
					largeFileInfo.FileSizeBytes = fileInfo.Length;
					m_LargeFiles.Add(largeFileInfo);
				}
			}

			m_LargeFiles.Sort((a, b) => b.FileSizeBytes.CompareTo(a.FileSizeBytes));
			m_LastScanMessage = "Scanned files: " + scannedFileCount + ", Threshold: " + m_ThresholdMb + "MB";
			Debug.Log("Large file check finished. " + m_LastScanMessage + ", Matched files: " + m_LargeFiles.Count);
		}

		private IEnumerable<string> EnumerateAllFiles(string rootPath)
		{
			Stack<string> pendingDirectories = new Stack<string>();
			pendingDirectories.Push(rootPath);

			while (pendingDirectories.Count > 0)
			{
				string currentDirectory = pendingDirectories.Pop();
				string[] childDirectories;
				try
				{
					childDirectories = Directory.GetDirectories(currentDirectory);
				}
				catch (Exception)
				{
					continue;
				}

				for (int i = 0; i < childDirectories.Length; i++)
				{
					if (ShouldSkipDirectory(childDirectories[i]))
					{
						continue;
					}
					pendingDirectories.Push(childDirectories[i]);
				}

				string[] files;
				try
				{
					files = Directory.GetFiles(currentDirectory);
				}
				catch (Exception)
				{
					continue;
				}

				for (int i = 0; i < files.Length; i++)
				{
					yield return files[i];
				}
			}
		}

		private bool ShouldSkipDirectory(string directoryPath)
		{
			if (!m_IgnoreGeneratedFolders)
			{
				return false;
			}

			string directoryName = Path.GetFileName(directoryPath);
			for (int i = 0; i < IgnoreDirectoryNames.Length; i++)
			{
				if (string.Equals(directoryName, IgnoreDirectoryNames[i], StringComparison.OrdinalIgnoreCase))
				{
					return true;
				}
			}

			return false;
		}

		private static string ToRelativePath(string fullPath, string projectRootPath)
		{
			string normalizedFullPath = fullPath.Replace('\\', '/');
			string normalizedRootPath = projectRootPath.Replace('\\', '/').TrimEnd('/');
			if (normalizedFullPath.StartsWith(normalizedRootPath, StringComparison.OrdinalIgnoreCase))
			{
				return normalizedFullPath.Substring(normalizedRootPath.Length + 1);
			}
			return normalizedFullPath;
		}

		private static string FormatBytes(long bytes)
		{
			double mb = bytes / 1024d / 1024d;
			return mb.ToString("F2") + " MB";
		}

		private static void SelectAsset(string relativePath)
		{
			if (string.IsNullOrEmpty(relativePath))
			{
				return;
			}

			UnityEngine.Object asset = AssetDatabase.LoadAssetAtPath<UnityEngine.Object>(relativePath);
			if (asset != null)
			{
				Selection.activeObject = asset;
				EditorGUIUtility.PingObject(asset);
				return;
			}

			Debug.LogWarning("Cannot select this file in Project view: " + relativePath);
		}
	}
}

