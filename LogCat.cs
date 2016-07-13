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
	string pathToAdb;
	const string _logRaw = "/logcat_raw.txt";
	const string _logFormatted = "/logcat_formatted.txt";
	//
	string path_logRaw;
	string path_logFormatted;
	//
	static Dictionary<string,Process> adbProcessList = new Dictionary<string, Process>();
	//
	Vector2 scrollViewPosition;
	//
	bool skipThisGuiFrame = false;
	bool connectedToDevice = false;
	//
	GUIStyle guistyle_normal;
	GUIStyle guistyle_important;
	GUIStyle guistyle_minor;
	//
	IEnumerator testConnectedToDevice;
	//

	#endregion
	#region editor window methods

	void Update () {
		skipThisGuiFrame = false;
		testConnectedToDevice.MoveNext();
	}

	void OnGUI () {
		//
		if( skipThisGuiFrame==true ) {
			return;
		}
		//
		if( path_logRaw==null ) {
			path_logRaw = string.Format( "{0}{1}" , Application.temporaryCachePath , _logRaw );
		}
		if( path_logFormatted==null ) {
			path_logFormatted = string.Format( "{0}{1}" , Application.temporaryCachePath , _logFormatted );
		}

		EditorGUILayout.BeginHorizontal();
		{
			if( GUILayout.Button( "|" , GUILayout.Width( 10f ) ) ) {
				Debug.Log( Application.temporaryCachePath );
			}
			//
			if( GUILayout.Button( "Reset" ) ) {
				StartAdbLogcatHandling();
			}

			EditorGUILayout.Space();

			if( GUILayout.Button( connectedToDevice==true ? "connected to ip:" : "connect to ip:" ) ) {
				//
				System.Net.IPAddress ip = GetMyDeviceIp();
				if( ip!=null ) {
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
			System.Net.IPAddress getDeviceIp = GetMyDeviceIp();
			EditorPrefs.SetString( "logcat_device_ip" , EditorGUILayout.TextField( getDeviceIp!=null ? getDeviceIp.ToString() : "enter ip address" , GUILayout.MaxWidth( 100f ) ) );
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
						if( lineUpperCases.Contains( "MISSING" ) || lineUpperCases.Contains( "NULL" ) || lineUpperCases.Contains( "EXCEPTION" ) || lineUpperCases.Contains( "ERROR" ) || lineUpperCases.Contains( "UNABLE TO" ) || lineUpperCases.Contains( "CAN'T" ) || lineUpperCases.Contains( "CANNOT" ) ) {
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
		foreach( string s in adbProcessList.Keys ) {
			EditorGUILayout.LabelField( s );
		}
	}

	/// <summary> window initialization </summary>
	void OnEnable () {
		//
		this.minSize = new Vector2( 100f , 100f );
		this.titleContent = new GUIContent( "LogCat" );
		//
		this.InitializeDirectoryPaths();
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
				//
				if( adbProcessList[giveProcessUniqueLabel].HasExited==false ) {
					adbProcessList[giveProcessUniqueLabel].CloseMainWindow();
					adbProcessList[giveProcessUniqueLabel].Close();
					adbProcessList.Remove( giveProcessUniqueLabel );
				}
				else {
					Debug.LogError( "proces \'"+giveProcessUniqueLabel+"\' się zakończył a nadal jest na liście" );
				}
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
		p.StartInfo.FileName = pathToAdb;
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
		if( System.IO.File.Exists( path_logRaw )==true ) {
			System.IO.File.Delete( path_logRaw );
		}
		if( System.IO.File.Exists( path_logFormatted )==true ) {
			System.IO.File.Delete( path_logFormatted );
		}
		StartAdbProcess(
			"logcat" ,
			"logcat -s Unity" ,
			new DataReceivedEventHandler( OutputHandler ) ,
			false
		);
		testConnectedToDevice = Enumerator_CheckIsMyDeviceConnected();
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

	System.Net.IPAddress GetMyDeviceIp () {
		System.Net.IPAddress ip;
		System.Net.IPAddress.TryParse( EditorPrefs.GetString( "logcat_device_ip" ) , out ip );
		return ip;
	}

	void InitializeDirectoryPaths () {
		//path to adb:
		pathToAdb = UnityEditor.EditorPrefs.GetString( "AndroidSdkRoot" )+@"\platform-tools\adb.exe";
		//make sure temp directory exists:
		string path = Application.temporaryCachePath;
		if( System.IO.Directory.Exists( path )==false ) {
			System.IO.Directory.CreateDirectory( path );
			Debug.Log( "logcat temp folder created: "+path );
		}
		//
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

	IEnumerator Enumerator_CheckIsMyDeviceConnected () {
		while( true ) {
			if( adbProcessList.ContainsKey( "devices" )==false ) {
				//
				System.Net.IPAddress ip = GetMyDeviceIp();
				//ask adb:
				StartAdbProcess(
					"devices" ,
					"devices" ,
					(sender , output ) => {
						//asses if connected:
						if( ip!=null && output.Data.Contains( ip.ToString() )==true ) {
							connectedToDevice = true;
						}
						else {
							connectedToDevice = false;
						}
					} ,
					false
				);
			}
			//yield:
			for( int i = 0 ; i<100 ; i++ ) {
				yield return 0;
			}
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