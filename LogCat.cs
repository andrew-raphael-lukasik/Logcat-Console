using UnityEngine;
using UnityEditor;
using System;
using System.Collections;
using System.Collections.Generic;
using System.Diagnostics;
using Debug = UnityEngine.Debug;
 
public class LogCat : EditorWindow {
	#region fields

	//
	readonly string _pathToAdb = @"D:\ProgramData\Android\SDK\platform-tools\adb.exe";
	const string _projectSubFolder = "/logcat";
	const string _logRaw = "/raw.txt";
	const string _logFormatted = "/formatted.txt";
	//
	string path_logRaw;
	string path_logFormatted;
	//
	static Dictionary<string,Process> adbProcessList = new Dictionary<string, Process>();
	//
	Vector2 scrollViewPosition;
	//
	bool skipThisGuiFrame = false;
	//
	GUIStyle guistyle_normal;
	GUIStyle guistyle_important;
	GUIStyle guistyle_minor;

	#endregion
	#region editor window methods

	void Update () {
		skipThisGuiFrame = false;
	}

	void OnGUI () {
		//
		if( skipThisGuiFrame==true ) {
			return;
		}
		//
		if( path_logRaw==null ) {
			path_logRaw = string.Format( "{0}{1}{2}" , Application.temporaryCachePath , _projectSubFolder , _logRaw );
		}
		if( path_logFormatted==null ) {
			path_logFormatted = string.Format( "{0}{1}{2}" , Application.temporaryCachePath , _projectSubFolder , _logFormatted );
		}

		EditorGUILayout.BeginHorizontal();
		{
			if( GUILayout.Button( "|" , GUILayout.Width( 10f ) ) ) {
				Debug.Log( Application.temporaryCachePath+_projectSubFolder );
			}
			//
			if( GUILayout.Button( "Restart Logcat process" ) ) {
				if( System.IO.File.Exists( path_logRaw )==true ) {
					System.IO.File.Delete( path_logRaw );
				}
				if( System.IO.File.Exists( path_logFormatted )==true ) {
					System.IO.File.Delete( path_logFormatted );
				}
				StartAdbLogcatHandling();
			}

			GUILayout.Label( adbProcessList.ContainsKey( "logcat" )==true ? (adbProcessList["logcat"].HasExited ? "0" : "1") : "-" );

			//
			if( GUILayout.Button( "log devices" ) ) {
				string devicesResult = "";
				StartAdbProcess(
					"devices" ,
					"devices" ,
					(sender , output ) => devicesResult += output.Data ,
					true ,
					1000*5
				);
				Debug.Log( devicesResult );
			}

			EditorGUILayout.Space();

			if( GUILayout.Button( "connect to ip:" ) ) {
				//
				System.Net.IPAddress ip;
				System.Net.IPAddress.TryParse( EditorPrefs.GetString( "logcat_device_ip" ) , out ip );
				if( ip!=null ) {
					//
					Debug.Log( "ip = "+ip.ToString() );
					//
					StartAdbProcess(
						"set tcpip port to 5555" ,
						"tcpip 5555" ,
						(sender , output ) => {
							if( output.Data.Equals( "Null" )==false )
								Debug.Log( output.Data );
						} ,
						true ,
						1000*1
					);
					StartAdbProcess(
						"connect to device ip" ,
						"connect "+ip.ToString() ,
						(sender , output ) => {
							if( output.Data.Equals( "Null" )==false )
								Debug.Log( output.Data );
						} ,
						true ,
						1000*10
					);
				}
				else {
					Debug.LogWarning( "enter valid ip address" );
				}
			}
			System.Net.IPAddress deviceIp;
			System.Net.IPAddress.TryParse( EditorPrefs.GetString( "logcat_device_ip" ) , out deviceIp );
			EditorPrefs.SetString( "logcat_device_ip" , EditorGUILayout.TextField( deviceIp!=null ? deviceIp.ToString() : "enter ip address" , GUILayout.MaxWidth( 100f ) ) );
		}
		EditorGUILayout.EndHorizontal();

		//
		scrollViewPosition = EditorGUILayout.BeginScrollView( scrollViewPosition );
		{
			if( System.IO.File.Exists( path_logFormatted )==true ) {
				foreach( string line in System.IO.File.ReadAllLines( path_logFormatted ) ) {
					if( line.Length==2 ) {
						EditorGUILayout.Space();
						EditorGUILayout.Space();
						EditorGUILayout.Space();
						EditorGUILayout.Space();
					}
					else {
						GUIStyle guistyle = guistyle_normal;
						string lineUpperCases = line.ToUpper();
						/*if( line.Contains( "(Filename:" ) ) {
							guistyle = guistyle_minor;
						}
						else*/
						if( lineUpperCases.Contains( "MISSING" ) || lineUpperCases.Contains( "NULL" ) || lineUpperCases.Contains( "EXCEPTION" ) || lineUpperCases.Contains( "ERROR" ) || lineUpperCases.Contains( "UNABLE TO" ) ) {
							guistyle = guistyle_important;
						}
						//
						if( GUILayout.Button( line , guistyle , GUILayout.ExpandWidth( false ) ) ) {
							EditorGUIUtility.systemCopyBuffer = line;
						}
					}
				}
			}
			GUILayout.FlexibleSpace();
		}
		EditorGUILayout.EndScrollView();
	}

	/// <summary> window initialization </summary>
	void OnEnable () {
		//
		this.minSize = new Vector2( 100f , 100f );
		this.titleContent = new GUIContent( "LogCat" );
		//
		this.MakeSureTempFolderExists();
		this.StartAdbLogcatHandling();
		//
		InitializeGuiStyles();
	}

	/// <summary> raised in event of RECOMPILATION </summary>
	void OnDisable () {
		foreach( Process p in adbProcessList.Values ) {
			if( p.HasExited==false ) {
				p.CloseMainWindow();
				p.Close();
			}
		}
		adbProcessList.Clear();
		//
		InitializeGuiStyles();
	}

	/// <summary> raised wnen window is about to be closed </summary>
	void OnDestroy () {
		foreach( Process p in adbProcessList.Values ) {
			if( p.HasExited==false ) {
				p.CloseMainWindow();
				p.Close();
			}
		}
		adbProcessList.Clear();
	}

	#endregion
	#region private methods

	void StartAdbProcess ( string giveProcessUniqueLabel , string argumentsForAdbExe , DataReceivedEventHandler outputHandler , bool argWaitForExit = false , int argWaitForExitMiliseconds = 10000 ) {
		// create process:
		Process p = new Process();
		// maintain  dictionary of asynchronous processes:
		if( argWaitForExit==false ) {
			//
			if( adbProcessList.ContainsKey( giveProcessUniqueLabel )==true ) {
				if( adbProcessList[giveProcessUniqueLabel].HasExited ) {
					Debug.LogError( "proces się zakończył a nadal jest na liście" );
				}
				adbProcessList[giveProcessUniqueLabel].CloseMainWindow();
				adbProcessList[giveProcessUniqueLabel].Close();
				adbProcessList.Remove( giveProcessUniqueLabel );
			}
			//add process to list and setup its exit action:
			adbProcessList.Add( giveProcessUniqueLabel , p );
			p.Exited += (sender , e ) => {
				Process senderProcess = (Process)sender;
				foreach( KeyValuePair<string,Process> item in adbProcessList ) {
					if( item.Value==senderProcess ) {
						adbProcessList.Remove( item.Key );
						break;
					}
				}
				Debug.LogError( "lista nie zawierała tego obiektu a powinna" );
			};
		}
		//
		p.StartInfo.FileName = _pathToAdb;
		p.StartInfo.Arguments = argumentsForAdbExe;//example: "logcat -s Unity"
		p.StartInfo.UseShellExecute = false;
		p.StartInfo.RedirectStandardOutput = true;
		p.StartInfo.RedirectStandardError = true;
		p.StartInfo.WindowStyle = ProcessWindowStyle.Hidden;
		p.StartInfo.CreateNoWindow = true;
		// Set your output and error (asynchronous) handlers
		p.OutputDataReceived += outputHandler;//(s, e) => Console.WriteLine(e.Data);
		p.ErrorDataReceived += outputHandler;//(s, e) => Console.WriteLine(e.Data);
		// Start process and handlers
		p.Start();
		p.BeginOutputReadLine();
		p.BeginErrorReadLine();
		if( argWaitForExit==true ) {
			p.WaitForExit( argWaitForExitMiliseconds );
		}
	}

	void StartAdbLogcatHandling () {
		StartAdbProcess(
			"logcat" ,
			"logcat -s Unity" ,
			new DataReceivedEventHandler( OutputHandler ) ,
			false
		);
	}

	void OutputHandler ( object sendingProcess , DataReceivedEventArgs output ) {
		//
		skipThisGuiFrame = true;
		//
		string raw = output.Data;
		//Debug.Log( output.Data );

		//
		/*if( ((int)(char)output.Data[output.Data.Length-1])==13 ) {
			lineRaw = lineRaw.Remove( lineRaw.Length-1 , 1 );
		}*/

		//
		System.IO.File.AppendAllText( path_logRaw , raw );


		//
		string formatted = string.Copy( raw );
		//formatted = "#"+formatted;
		//formatted = "<START>"+formatted;
		//formatted = formatted.Replace( new String( new char[]{ (char)13, (char)10 } ) , " |nr " );
		//formatted = formatted.Replace( "\n\r\n\r" , " |nrnr " );
		//formatted = formatted.Replace( "\r\r\r\r" , " |rrrr " );
		formatted = formatted.Replace( "\r\r" , "" );//formatted.Replace( "\r\r" , " |rr " );
		//formatted = formatted.Replace( "\n" , " |n " );
		//formatted = formatted.Replace( "\r" , " |r " );
		//formatted = formatted.Replace( "\n\r" , " | " );
		formatted += "\n";//"<END>\n";
		/*string formatted = "";
		foreach( char c in raw ) {
			formatted += ","+(int)c;
		}*/
		//discard lines with no useful info:  
		if( formatted.Contains( "(Filename: " )==false ) {
			//is this line reported in Unity format?:
			if( formatted.Contains( "Unity   :" )==true ) {
				int clampLeft = 42;
				if( formatted.Length>clampLeft ) {
					try {
						formatted = formatted.Substring( clampLeft , formatted.Length-clampLeft );
					} catch ( Exception ex ) {
						Debug.LogException( ex );
					}
				}
				else
					Debug.LogError( "najn!" );
			}

			if( formatted.Length>2 ) {
				System.IO.File.AppendAllText( path_logFormatted , formatted );
			}
		}
	}

	void MakeSureTempFolderExists () {
		string path = Application.temporaryCachePath+_projectSubFolder;
		if( System.IO.Directory.Exists( path )==false ) {
			System.IO.Directory.CreateDirectory( path );
			Debug.Log( "logcat temp folder created: "+path );
		}
	}

	void InitializeGuiStyles () {
		{
			guistyle_normal = new GUIStyle();
			guistyle_normal.normal.textColor = new Color( 0.7f , 0.7f , 0.7f );
			guistyle_normal.active.textColor = Color.white;
		}
		{
			guistyle_minor = new GUIStyle();
			guistyle_minor.normal.textColor = Color.grey;
			guistyle_minor.active.textColor = Color.white;
		}
		{
			guistyle_important = new GUIStyle();
			guistyle_important.normal.textColor = new Color( 0.7f , 0f , 0f );
			guistyle_important.active.textColor = Color.white;
		}
	}

	#endregion
	#region public methods

	[MenuItem( "Window/LogCat" )]
	public static void CreateWindow () {
		LogCat window = (LogCat)EditorWindow.GetWindow( typeof(LogCat) );
		window.Show();
	}

	#endregion
}