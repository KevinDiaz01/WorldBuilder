using System;
using System.IO;
using System.Security.Cryptography;
using System.Text;
using UnityEngine;

namespace WorldBuilder {
 [Serializable]
 public sealed class RecoverySnapshot {
  public string sourcePath;
  public string savedUtc;
  public string projectJson;
  public string checksum;
  public int objectCount;
 }

 // Crash-safe local recovery independent of normal project saves.
 public static class RecoveryStore {
  static string Root {
   get {
    string folder=Path.Combine(Application.persistentDataPath,"Recovery");
    Directory.CreateDirectory(folder);
    return folder;
   }
  }
  static string SessionMarker { get { return Path.Combine(Root,"active-session.txt"); } }
  static string Hash(string value){
   using(var sha=SHA256.Create()){
    byte[] bytes=sha.ComputeHash(Encoding.UTF8.GetBytes(value??""));
    return BitConverter.ToString(bytes).Replace("-","").ToLowerInvariant();
   }
  }
  static string SnapshotFile(string projectPath){
   // Absolute project paths keep similarly named worlds in different folders separate.
   return Path.Combine(Root,"project-"+Hash(Path.GetFullPath(projectPath)).Substring(0,24)+".json");
  }
  public static void WriteAtomic(string destination,string content){
   if(string.IsNullOrWhiteSpace(destination))throw new ArgumentException("Missing destination path");
   Directory.CreateDirectory(Path.GetDirectoryName(Path.GetFullPath(destination)));
   string temp=destination+".writing-"+Guid.NewGuid().ToString("N");
   try{
    // Flush the new file before replacing the previous good copy.
    using(var stream=new FileStream(temp,FileMode.CreateNew,FileAccess.Write,FileShare.None)){
     byte[] bytes=new UTF8Encoding(false).GetBytes(content);
     stream.Write(bytes,0,bytes.Length);
     stream.Flush(true);
    }
    if(File.Exists(destination)){
     string backup=destination+".bak";
     try{
      File.Replace(temp,destination,backup);
     }catch(PlatformNotSupportedException){
      File.Copy(destination,backup,true);
      File.Copy(temp,destination,true);
      File.Delete(temp);
     }
    }else File.Move(temp,destination);
   }finally{
    if(File.Exists(temp))File.Delete(temp);
   }
  }
  public static RecoverySnapshot FindInterrupted(){
   if(!File.Exists(SessionMarker))return null;
   try{
    string projectPath=File.ReadAllText(SessionMarker).Trim();
    RecoverySnapshot candidate=ReadLatest(projectPath);
    if(candidate==null)return null;
    if(File.Exists(projectPath)){
     string saved=File.ReadAllText(projectPath);
     if(Hash(saved)==candidate.checksum)return null;
     // A normal manual save newer than this recovery is the safer source.
     if(File.GetLastWriteTimeUtc(projectPath)>=DateTime.Parse(candidate.savedUtc).ToUniversalTime())return null;
    }
    return candidate;
   }catch(Exception ex){
    Debug.LogWarning("WorldBuilder recovery check: "+ex.Message);
    return null;
   }
  }
  public static void BeginSession(string path){
   WriteAtomic(SessionMarker,Path.GetFullPath(path));
  }
  public static void ChangeProject(string path){
   WriteAtomic(SessionMarker,Path.GetFullPath(path));
  }
  public static void EndSession(){
   if(File.Exists(SessionMarker))File.Delete(SessionMarker);
  }
  static RecoverySnapshot Validate(string content){
   RecoverySnapshot candidate=JsonUtility.FromJson<RecoverySnapshot>(content);
   if(candidate==null||string.IsNullOrEmpty(candidate.sourcePath)||string.IsNullOrEmpty(candidate.projectJson)||
      candidate.checksum!=Hash(candidate.projectJson))return null;
   Project data=JsonUtility.FromJson<Project>(candidate.projectJson);
   if(data==null||data.schema!=2||data.pieces==null)return null;
   return candidate;
  }
  public static RecoverySnapshot ReadLatest(string projectPath){
   if(string.IsNullOrWhiteSpace(projectPath))return null;
   string file=SnapshotFile(projectPath);
   foreach(string candidatePath in new[]{file,file+".bak"}){
    if(!File.Exists(candidatePath))continue;
    try{
     RecoverySnapshot snapshot=Validate(File.ReadAllText(candidatePath));
     if(snapshot!=null&&string.Equals(Path.GetFullPath(snapshot.sourcePath),
        Path.GetFullPath(projectPath),StringComparison.OrdinalIgnoreCase))return snapshot;
    }catch(Exception ex){Debug.LogWarning("Recovery snapshot invalid: "+ex.Message);}
   }
   // Compatibility with snapshots written by earlier WorldBuilder versions.
   string legacy=Path.Combine(Root,"autosave.json");
   if(File.Exists(legacy)){
    try{
     string json=File.ReadAllText(legacy);
     Project project=JsonUtility.FromJson<Project>(json);
     if(project!=null&&project.schema==2&&project.pieces!=null){
      return new RecoverySnapshot{sourcePath=projectPath,projectJson=json,
       savedUtc=File.GetLastWriteTimeUtc(legacy).ToString("o"),checksum=Hash(json),
       objectCount=project.pieces.Count};
     }
    }catch(Exception ex){Debug.LogWarning("Legacy recovery invalid: "+ex.Message);}
   }
   return null;
  }
  public static bool WriteSnapshot(string projectPath,string projectJson,int objectCount){
   if(string.IsNullOrWhiteSpace(projectPath)||string.IsNullOrWhiteSpace(projectJson))return false;
   string checksum=Hash(projectJson);
   RecoverySnapshot old=ReadLatest(projectPath);
   if(old!=null&&old.checksum==checksum)return false;
   var snapshot=new RecoverySnapshot{sourcePath=Path.GetFullPath(projectPath),
    projectJson=projectJson,savedUtc=DateTime.UtcNow.ToString("o"),checksum=checksum,objectCount=objectCount};
   WriteAtomic(SnapshotFile(projectPath),JsonUtility.ToJson(snapshot));
   return true;
  }
 }
}