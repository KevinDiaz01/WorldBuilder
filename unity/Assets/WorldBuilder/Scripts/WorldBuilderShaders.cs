using System;
using UnityEngine;

namespace WorldBuilder {
 // Explicit Resources shader assets survive Unity's standalone player stripping.
 public static class WorldBuilderShaders {
  static Shader lit;
  static Shader overlay;
  public static Shader Lit {
   get {
    if(lit==null)lit=Resources.Load<Shader>("WorldBuilderLit");
    if(lit==null)throw new InvalidOperationException("WorldBuilderLit shader missing from install");
    return lit;
   }
  }
  public static Shader Overlay {
   get {
    if(overlay==null)overlay=Resources.Load<Shader>("WorldBuilderOverlay");
    if(overlay==null)throw new InvalidOperationException("WorldBuilderOverlay shader missing from install");
    return overlay;
   }
  }
  public static Material NewLit(Color color) {
   Material m=new Material(Lit);m.color=color;return m;
  }
  public static Material NewOverlay(Color color) {
   Material m=new Material(Overlay);m.color=color;return m;
  }
 }
}
