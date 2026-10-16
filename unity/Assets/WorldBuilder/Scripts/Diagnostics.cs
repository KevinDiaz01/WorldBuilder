using System;
using System.Collections.Generic;
using UnityEngine;

namespace WorldBuilder {
 // Bounded, in-session diagnostic event journal displayed by F2.
 public static class Diagnostics {
  public struct Entry {
   public string time;
   public string level;
   public string message;
  }
  static readonly List<Entry> entries=new List<Entry>();
  public static int Errors {get;private set;}
  public static int Warnings {get;private set;}
  public static IList<Entry> Entries {get{return entries.AsReadOnly();}}
  public static void Add(string level,string message){
   if(level=="ERROR")Errors++;
   else if(level=="WARN")Warnings++;
   entries.Add(new Entry{time=DateTime.Now.ToString("HH:mm:ss"),level=level,message=message});
   if(entries.Count>150)entries.RemoveAt(0);
  }
  public static void Clear(){entries.Clear();Errors=0;Warnings=0;}
 }
}