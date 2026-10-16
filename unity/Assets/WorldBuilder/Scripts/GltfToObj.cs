using System;
using System.IO;
using System.Text;
using System.Collections.Generic;
using System.Globalization;
using UnityEngine;

namespace WorldBuilder {
 // Converts a supported static glTF 2.0 mesh to OBJ so the existing editor,
 // asset catalog and VRChat importer all use one reliable interchange format.
 // Supports GLB BIN chunks, external/data URI buffers, triangles, UVs and node transforms.
 // Skins, animations, Draco, meshopt and compressed textures require a later importer.
 public static class GltfToObj {
  [Serializable] class Document {
   public Buffer[] buffers; public View[] bufferViews; public Accessor[] accessors;
   public Mesh[] meshes; public Node[] nodes; public Scene[] scenes; public int scene; public GltfMaterial[] materials; public GltfTexture[] textures; public GltfImage[] images;
  }
  [Serializable] class Buffer {public string uri; public int byteLength;}
  [Serializable] class View {public int buffer;public int byteOffset;public int byteLength;public int byteStride;}
  [Serializable] class Accessor {public int bufferView=-1;public int byteOffset;public int componentType;public int count;public string type;public bool normalized;}
  [Serializable] class Mesh {public Primitive[] primitives;}
  [Serializable] class Primitive {public Attributes attributes;public int indices=-1;public int mode=4;}
  [Serializable] class Attributes {public int POSITION=-1;public int NORMAL=-1;public int TEXCOORD_0=-1;}
  [Serializable] class Node {public int mesh=-1;public int[] children;public float[] translation;public float[] rotation;public float[] scale;public float[] matrix;}
  [Serializable] class Scene {public int[] nodes;}
  [Serializable] class GltfMaterial {public Pbr pbrMetallicRoughness;}
  [Serializable] class Pbr {public float[] baseColorFactor;public TextureIndex baseColorTexture;}
  [Serializable] class TextureIndex {public int index=-1;}
  [Serializable] class GltfTexture {public int source=-1;}
  [Serializable] class GltfImage {public string uri;public string mimeType;public int bufferView=-1;}
  static readonly CultureInfo CI=CultureInfo.InvariantCulture;
  static string N(float x){return x.ToString("R",CI);}
  static string Resolve(string directory,string uri){
   string decoded=Uri.UnescapeDataString(uri.Replace('/',
    Path.DirectorySeparatorChar));
   string result=Path.GetFullPath(Path.Combine(directory,decoded));
   string root=Path.GetFullPath(directory).TrimEnd(Path.DirectorySeparatorChar)+Path.DirectorySeparatorChar;
   if(!result.StartsWith(root,StringComparison.OrdinalIgnoreCase))
    throw new InvalidDataException("glTF references a file outside the model directory");
   return result;
  }
  static byte[] BufferData(string uri,string dir,byte[] bin){
   if(string.IsNullOrEmpty(uri)){
    if(bin==null)throw new InvalidDataException("Missing GLB binary buffer");
    return bin;
   }
   if(uri.StartsWith("data:",StringComparison.OrdinalIgnoreCase)){
    int comma=uri.IndexOf(',');
    if(comma<0||!uri.Substring(0,comma).EndsWith(";base64",StringComparison.OrdinalIgnoreCase))
     throw new InvalidDataException("Only base64 embedded glTF buffers are supported");
    return Convert.FromBase64String(uri.Substring(comma+1));
   }
   if(uri.Contains("://")||uri.StartsWith("//"))throw new InvalidDataException("Remote glTF buffers are unsupported");
   return File.ReadAllBytes(Resolve(dir,uri));
  }
  static int ComponentSize(int type){
   switch(type){case 5120:case 5121:return 1;case 5122:case 5123:return 2;case 5125:case 5126:return 4;}
   throw new InvalidDataException("Unsupported glTF component type "+type);
  }
  static int Components(string type){
   switch(type){case "SCALAR":return 1;case "VEC2":return 2;case "VEC3":return 3;case "VEC4":return 4;}
   throw new InvalidDataException("Unsupported glTF accessor "+type);
  }
  static float ReadComponent(byte[] bytes,int offset,int type,bool normalized){
   switch(type){
    case 5126:return BitConverter.ToSingle(bytes,offset);
    case 5121:return normalized?bytes[offset]/255f:bytes[offset];
    case 5120:return normalized?Mathf.Max(-1f,(sbyte)bytes[offset]/127f):(sbyte)bytes[offset];
    case 5123:{ushort v=BitConverter.ToUInt16(bytes,offset);return normalized?v/65535f:v;}
    case 5122:{short v=BitConverter.ToInt16(bytes,offset);return normalized?Mathf.Max(-1f,v/32767f):v;}
    case 5125:return BitConverter.ToUInt32(bytes,offset);
   }
   throw new InvalidDataException("Unsupported accessor data");
  }
  static float[][] ReadAccessor(Document doc,byte[][] buffers,int index){
   if(index<0||index>=doc.accessors.Length)throw new InvalidDataException("Invalid accessor index");
   Accessor a=doc.accessors[index];
   if(a.bufferView<0||a.bufferView>=doc.bufferViews.Length)throw new InvalidDataException("Sparse accessors not yet supported");
   View view=doc.bufferViews[a.bufferView];
   if(view.buffer<0||view.buffer>=buffers.Length)throw new InvalidDataException("Invalid glTF buffer");
   int count=Components(a.type),unit=ComponentSize(a.componentType),size=count*unit;
   int stride=view.byteStride>0?view.byteStride:size;
   if(stride<size||a.count<0||a.count>2000000)throw new InvalidDataException("Unsupported accessor stride or size");
   int offset=checked(view.byteOffset+a.byteOffset);
   byte[] source=buffers[view.buffer];
   long final= (long)offset+(long)Math.Max(0,a.count-1)*stride+size;
   long viewEnd=(long)view.byteOffset+view.byteLength;
   if(offset<view.byteOffset||final>viewEnd||final>source.LongLength)throw new InvalidDataException("Accessor outside glTF buffer");
   var result=new float[a.count][];
   for(int i=0;i<a.count;i++){
    result[i]=new float[count];
    for(int c=0;c<count;c++)result[i][c]=ReadComponent(source,offset+i*stride+c*unit,a.componentType,a.normalized);
   }
   return result;
  }
  static Matrix4x4 Transform(Node n){
   if(n.matrix!=null&&n.matrix.Length==16){
    var m=new Matrix4x4();
    for(int col=0;col<4;col++)for(int row=0;row<4;row++)m[row,col]=n.matrix[col*4+row];
    return m;
   }
   Vector3 t=n.translation!=null&&n.translation.Length>=3?new Vector3(n.translation[0],n.translation[1],n.translation[2]):Vector3.zero;
   Vector3 sc=n.scale!=null&&n.scale.Length>=3?new Vector3(n.scale[0],n.scale[1],n.scale[2]):Vector3.one;
   Quaternion q=n.rotation!=null&&n.rotation.Length>=4?new Quaternion(n.rotation[0],n.rotation[1],n.rotation[2],n.rotation[3]):Quaternion.identity;
   return Matrix4x4.TRS(t,q,sc);
  }
  public static void ConvertModel(string input,string output){
   string json;byte[] bin=null;
   if(Path.GetExtension(input).Equals(".glb",StringComparison.OrdinalIgnoreCase)){
    byte[] data=File.ReadAllBytes(input);
    if(data.Length<20||BitConverter.ToUInt32(data,0)!=0x46546C67||BitConverter.ToUInt32(data,4)!=2)
     throw new InvalidDataException("Expected a glTF 2.0 GLB file");
    if(BitConverter.ToUInt32(data,8)!=data.Length)throw new InvalidDataException("Invalid GLB length");
    int cursor=12;json=null;
    while(cursor+8<=data.Length){
     int size=checked((int)BitConverter.ToUInt32(data,cursor));
     uint type=BitConverter.ToUInt32(data,cursor+4);cursor+=8;
     if(size<0||(long)cursor+size>data.Length)throw new InvalidDataException("Invalid GLB chunk");
     if(type==0x4E4F534A)json=Encoding.UTF8.GetString(data,cursor,size).TrimEnd('\0',' ','\n');
     if(type==0x004E4942){bin=new byte[size];System.Buffer.BlockCopy(data,cursor,bin,0,size);}
     cursor+=size;
    }
    if(string.IsNullOrEmpty(json))throw new InvalidDataException("Missing GLB JSON chunk");
   }else json=File.ReadAllText(input);
   if(new FileInfo(input).Length>512L*1024*1024)throw new InvalidDataException("3D model exceeds 512 MB import limit");
   Document doc=JsonUtility.FromJson<Document>(json);
   if(doc==null||doc.buffers==null||doc.bufferViews==null||doc.accessors==null||doc.meshes==null)
    throw new InvalidDataException("glTF has no supported mesh data");
   byte[][] buffers=new byte[doc.buffers.Length][];
   string folder=Path.GetDirectoryName(input);
   for(int i=0;i<buffers.Length;i++){
    buffers[i]=BufferData(doc.buffers[i].uri,folder,bin);
    if(buffers[i].Length<doc.buffers[i].byteLength)throw new InvalidDataException("Incomplete glTF buffer");
   }
   var lines=new StringBuilder("# Converted by WorldBuilder glTF import\n");
   string materialText=null;
   if(doc.materials!=null&&doc.materials.Length>0){
    var pbr=doc.materials[0].pbrMetallicRoughness;
    Color baseColor=Color.white;
    if(pbr!=null&&pbr.baseColorFactor!=null&&pbr.baseColorFactor.Length>=3)
     baseColor=new Color(pbr.baseColorFactor[0],pbr.baseColorFactor[1],pbr.baseColorFactor[2]);
    var mtl=new StringBuilder("newmtl WorldBuilderBase\nKd "+N(baseColor.r)+" "+N(baseColor.g)+" "+N(baseColor.b)+"\n");
    if(pbr!=null&&pbr.baseColorTexture!=null&&doc.textures!=null&&doc.images!=null){
     int tex=pbr.baseColorTexture.index;
     if(tex>=0&&tex<doc.textures.Length){
      int imageIndex=doc.textures[tex].source;
      if(imageIndex>=0&&imageIndex<doc.images.Length){
       var pic=doc.images[imageIndex];
       byte[] picture=null;
       string extension=pic.mimeType=="image/jpeg"?".jpg":".png";
       if(!string.IsNullOrEmpty(pic.uri)){
        if(pic.uri.StartsWith("data:",StringComparison.OrdinalIgnoreCase)){
         int comma=pic.uri.IndexOf(',');
         if(comma>=0){
          extension=pic.uri.Substring(0,comma).Contains("jpeg")?".jpg":".png";
          picture=System.Convert.FromBase64String(pic.uri.Substring(comma+1));
         }
        }else if(!pic.uri.Contains("://")){
         string sourcePath=Resolve(folder,pic.uri);
         if(File.Exists(sourcePath)){
          picture=File.ReadAllBytes(sourcePath);
          extension=Path.GetExtension(sourcePath).ToLowerInvariant();
         }
        }
       }else if(pic.bufferView>=0&&pic.bufferView<doc.bufferViews.Length){
        View view=doc.bufferViews[pic.bufferView];
        byte[] buffer=buffers[view.buffer];
        if(view.byteOffset>=0&&view.byteLength>0&&(long)view.byteOffset+view.byteLength<=buffer.Length){
         picture=new byte[view.byteLength];
         System.Buffer.BlockCopy(buffer,view.byteOffset,picture,0,view.byteLength);
        }
       }
       if(picture!=null&&(extension==".png"||extension==".jpg"||extension==".jpeg")){
        string fileName="basecolor"+extension;
        File.WriteAllBytes(Path.Combine(Path.GetDirectoryName(output),fileName),picture);
        mtl.Append("map_Kd ").Append(fileName).Append('\n');
       }
      }
     }
    }
    materialText=mtl.ToString();
    lines.Append("mtllib model.mtl\nusemtl WorldBuilderBase\n");
   }
   int vertexBase=0,faces=0;
   Action<int,Matrix4x4> append=(meshIndex,transform)=>{
    if(meshIndex<0||meshIndex>=doc.meshes.Length)return;
    if(doc.meshes[meshIndex].primitives==null)return;
    foreach(var prim in doc.meshes[meshIndex].primitives){
     if(prim.mode!=4||prim.attributes==null||prim.attributes.POSITION<0)continue;
     var verts=ReadAccessor(doc,buffers,prim.attributes.POSITION);
     var uv=prim.attributes.TEXCOORD_0>=0?ReadAccessor(doc,buffers,prim.attributes.TEXCOORD_0):null;
     var normals=prim.attributes.NORMAL>=0?ReadAccessor(doc,buffers,prim.attributes.NORMAL):null;
     var indices=prim.indices>=0?ReadAccessor(doc,buffers,prim.indices):null;
     if(uv!=null&&uv.Length!=verts.Length)uv=null;
     if(normals!=null&&normals.Length!=verts.Length)normals=null;
     Matrix4x4 normMatrix=transform.inverse.transpose;
     lines.Append("o Mesh_").Append(meshIndex).Append('\n');
     foreach(var v in verts){
      var p=transform.MultiplyPoint3x4(new Vector3(v[0],v[1],v[2]));
      lines.Append("v ").Append(N(p.x)).Append(' ').Append(N(p.y)).Append(' ').Append(N(p.z)).Append('\n');
     }
     for(int k=0;k<verts.Length;k++){
      float[] t=uv==null?null:uv[k];
      lines.Append("vt ").Append(N(t==null?0f:t[0])).Append(' ').Append(N(t==null?0f:t[1])).Append('\n');
     }
     for(int k=0;k<verts.Length;k++){
      Vector3 n=normals==null?Vector3.up:normMatrix.MultiplyVector(new Vector3(normals[k][0],normals[k][1],normals[k][2])).normalized;
      lines.Append("vn ").Append(N(n.x)).Append(' ').Append(N(n.y)).Append(' ').Append(N(n.z)).Append('\n');
     }
     int nIndices=indices==null?verts.Length:indices.Length;
     for(int t=0;t+2<nIndices;t+=3){
      int[] ids=new int[3];bool valid=true;
      for(int z=0;z<3;z++){ids[z]=indices==null?t+z:(int)indices[t+z][0];if(ids[z]<0||ids[z]>=verts.Length)valid=false;}
      if(!valid)continue;
      lines.Append("f");
      foreach(int id in ids){int ix=vertexBase+id+1;lines.Append(' ').Append(ix).Append('/').Append(ix).Append('/').Append(ix);}
      lines.Append('\n');faces++;
     }
     vertexBase+=verts.Length;
    }
   };
   if(doc.nodes!=null&&doc.nodes.Length>0){
    var roots=new List<int>();
    if(doc.scenes!=null&&doc.scenes.Length>0){
     int ix=doc.scene>=0&&doc.scene<doc.scenes.Length?doc.scene:0;
     if(doc.scenes[ix].nodes!=null)roots.AddRange(doc.scenes[ix].nodes);
    }
    if(roots.Count==0){
     var childIds=new HashSet<int>();
     foreach(var node in doc.nodes)if(node.children!=null)foreach(int child in node.children)childIds.Add(child);
     for(int i=0;i<doc.nodes.Length;i++)if(!childIds.Contains(i))roots.Add(i);
    }
    int visited=0;
    Action<int,Matrix4x4,int> visit=null;
    visit=(nodeIndex,parent,depth)=>{
     if(nodeIndex<0||nodeIndex>=doc.nodes.Length||depth>100||++visited>30000)
      throw new InvalidDataException("Invalid or cyclic glTF node structure");
     var n=doc.nodes[nodeIndex];var world=parent*Transform(n);
     if(n.mesh>=0)append(n.mesh,world);
     if(n.children!=null)foreach(int child in n.children)visit(child,world,depth+1);
    };
    foreach(int ix in roots)visit(ix,Matrix4x4.identity,0);
   }else for(int i=0;i<doc.meshes.Length;i++)append(i,Matrix4x4.identity);
   if(faces==0)throw new InvalidDataException("No uncompressed triangle mesh found (Draco/meshopt are unsupported)");
   Directory.CreateDirectory(Path.GetDirectoryName(output));
   File.WriteAllText(output,lines.ToString());
   if(materialText!=null)File.WriteAllText(Path.Combine(Path.GetDirectoryName(output),"model.mtl"),materialText);
  }
 }
}