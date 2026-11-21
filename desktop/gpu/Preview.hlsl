cbuffer Params:register(b0){float2 viewport;int mode;float opacity;int4 offsets[16];float4 mapping;}
struct Vert{float4 p:SV_Position;float2 uv:TEXCOORD;float4 color:COLOR;float4 bounds:TEXCOORD1;};
Vert vs(uint id:SV_VertexID,float2 origin:POSITION,float4 axes:TEXCOORD0,float4 uv:TEXCOORD1,float4 color:COLOR){
 float2 corner=float2(id==1||id==4||id==5,id==2||id==3||id==5);
 float2 pixel=origin+axes.xy*corner.x+axes.zw*corner.y;
 Vert v;v.p=float4(pixel*float2(2,-2)/viewport+float2(-1,1),0,1);
 v.uv=lerp(uv.xy,uv.zw,corner);v.bounds=uv+float4(.5,.5,-.5,-.5)/2048;
 v.color=color;return v;
}
Vert screen(uint id:SV_VertexID){
 Vert v;v.p=float4(id==1?3:-1,id==2?-3:1,0,1);v.uv=0;v.color=1;v.bounds=0;return v;
}
Texture2D tex:register(t0);SamplerState pointSampler:register(s0);
float4 sprite(Vert v):SV_Target{return tex.Sample(pointSampler,clamp(v.uv,v.bounds.xy,v.bounds.zw))*v.color;}
SamplerState linearBorder:register(s1);
float4 resample(Vert v):SV_Target{
 uint w,h;tex.GetDimensions(w,h);
 float2 uv=(mapping.xy+v.p.xy*mapping.zw)/float2(w,h);
 return tex.SampleLevel(linearBorder,uv,0);
}
float4 post(Vert v):SV_Target{
 int2 p=(int2)v.p.xy;float4 orig=tex.Load(int3(p,0));
 if(mode==1){
 float2 shifted=float2(tex.Load(int3(p-offsets[0].xy,0)).r,tex.Load(int3(p+offsets[0].xy,0)).b);
 float2 rb=floor(lerp(orig.rb,shifted,opacity)*255.0+0.00001)/255.0;
 return float4(rb.x,orig.g,rb.y,1);
 }
 float3 col=0;
 for(int i=0;i<mode;i++){float4 s=tex.Load(int3(p-offsets[i].xy,0));col=lerp(col,s.rgb,s.a/(i+1));}
 return float4(lerp(col,orig.rgb,orig.a*(1-opacity)),1);
}
