#define NOMINMAX
#include <windows.h>
#include <d3d11.h>
#include <wrl/client.h>
#include <vector>
#include <algorithm>
#include <cmath>
#include <cstring>
using Microsoft::WRL::ComPtr;
static void check(HRESULT hr){if(FAILED(hr))throw hr;}
static const int atlasSize=2048;
#include "vs.h"
#include "screen.h"
#include "sprite.h"
#include "post.h"
#include "resample.h"

struct Instance{float x,y,axisX[2],axisY[2],uv[4];unsigned rgba;};
struct Quad{int tile;float x,y,sx,sy,angle,ox,oy;unsigned rgba;};
struct Tile{int x,y,w,h,page;};
struct Params{float w,h;int mode;float opacity;int offsets[16][4];float mapping[4];};
struct Target {
 int w=0,h=0;ComPtr<ID3D11ShaderResourceView> rendered;ComPtr<ID3D11Texture2D> output,readback,input;
 ComPtr<ID3D11ShaderResourceView> source;ComPtr<ID3D11RenderTargetView> view;
};
struct Page{ComPtr<ID3D11Texture2D> tex;ComPtr<ID3D11ShaderResourceView> view;int x=0,y=0,row=0;};
struct Device{
 ComPtr<ID3D11Device> d;ComPtr<ID3D11DeviceContext> c;
 ComPtr<ID3D11VertexShader> vs,screen;ComPtr<ID3D11PixelShader> ps,post,resample;
 ComPtr<ID3D11InputLayout> layout;ComPtr<ID3D11BlendState> alpha,premultiplied;
 ComPtr<ID3D11SamplerState> sampler,linear;ComPtr<ID3D11RasterizerState> raster,clipped;
 ComPtr<ID3D11Buffer> constants,vertices;size_t capacity=0;
 Target scene,fx,postA,postB,crop,stretch;ComPtr<ID3D11Texture2D> backdrop;int baseW=0,baseH=0;std::vector<Page> pages;std::vector<Tile> tiles;
 std::vector<Instance> stream;std::vector<std::pair<unsigned,int>> runs;
 Device(wchar_t* name,int length){
  const D3D_FEATURE_LEVEL levels[]={D3D_FEATURE_LEVEL_11_0,D3D_FEATURE_LEVEL_10_1,D3D_FEATURE_LEVEL_10_0};
  check(D3D11CreateDevice(nullptr,D3D_DRIVER_TYPE_HARDWARE,nullptr,D3D11_CREATE_DEVICE_BGRA_SUPPORT,levels,3,D3D11_SDK_VERSION,&d,nullptr,&c));
  ComPtr<IDXGIDevice> dx;ComPtr<IDXGIAdapter> adapter;DXGI_ADAPTER_DESC desc={};
  check(d.As(&dx));check(dx->GetAdapter(&adapter));check(adapter->GetDesc(&desc));
  if(desc.VendorId==0x1414&&desc.DeviceId==0x8c)throw E_FAIL;
  wcsncpy_s(name,length,desc.Description,_TRUNCATE);
  check(d->CreateVertexShader(code_vs,sizeof(code_vs),nullptr,&vs));
  check(d->CreateVertexShader(code_screen,sizeof(code_screen),nullptr,&screen));
  check(d->CreatePixelShader(code_sprite,sizeof(code_sprite),nullptr,&ps));
  check(d->CreatePixelShader(code_post,sizeof(code_post),nullptr,&post));
  check(d->CreatePixelShader(code_resample,sizeof(code_resample),nullptr,&resample));
  D3D11_INPUT_ELEMENT_DESC elements[]={
   {"POSITION",0,DXGI_FORMAT_R32G32_FLOAT,0,0,D3D11_INPUT_PER_INSTANCE_DATA,1},
   {"TEXCOORD",0,DXGI_FORMAT_R32G32B32A32_FLOAT,0,8,D3D11_INPUT_PER_INSTANCE_DATA,1},
   {"TEXCOORD",1,DXGI_FORMAT_R32G32B32A32_FLOAT,0,24,D3D11_INPUT_PER_INSTANCE_DATA,1},
   {"COLOR",0,DXGI_FORMAT_R8G8B8A8_UNORM,0,40,D3D11_INPUT_PER_INSTANCE_DATA,1}};
  check(d->CreateInputLayout(elements,4,code_vs,sizeof(code_vs),&layout));
  D3D11_BUFFER_DESC b={};b.ByteWidth=sizeof(Params);b.BindFlags=D3D11_BIND_CONSTANT_BUFFER;check(d->CreateBuffer(&b,nullptr,&constants));
  D3D11_RASTERIZER_DESC r={};r.FillMode=D3D11_FILL_SOLID;r.CullMode=D3D11_CULL_NONE;r.DepthClipEnable=TRUE;check(d->CreateRasterizerState(&r,&raster));r.ScissorEnable=TRUE;check(d->CreateRasterizerState(&r,&clipped));
  D3D11_SAMPLER_DESC sm={};sm.Filter=D3D11_FILTER_MIN_MAG_MIP_POINT;sm.AddressU=sm.AddressV=sm.AddressW=D3D11_TEXTURE_ADDRESS_CLAMP;sm.MaxLOD=D3D11_FLOAT32_MAX;check(d->CreateSamplerState(&sm,&sampler));
  sm.Filter=D3D11_FILTER_MIN_MAG_LINEAR_MIP_POINT;sm.AddressU=sm.AddressV=sm.AddressW=D3D11_TEXTURE_ADDRESS_BORDER;check(d->CreateSamplerState(&sm,&linear));
  D3D11_BLEND_DESC blend={};auto& a=blend.RenderTarget[0];a.BlendEnable=TRUE;a.SrcBlend=D3D11_BLEND_SRC_ALPHA;a.DestBlend=D3D11_BLEND_INV_SRC_ALPHA;a.BlendOp=a.BlendOpAlpha=D3D11_BLEND_OP_ADD;a.SrcBlendAlpha=D3D11_BLEND_ONE;a.DestBlendAlpha=D3D11_BLEND_INV_SRC_ALPHA;a.RenderTargetWriteMask=D3D11_COLOR_WRITE_ENABLE_ALL;check(d->CreateBlendState(&blend,&alpha));a.SrcBlend=D3D11_BLEND_ONE;check(d->CreateBlendState(&blend,&premultiplied));
 }
 ComPtr<ID3D11Texture2D> texture(int w,int h,UINT bind,bool staging=false){
  D3D11_TEXTURE2D_DESC t={};t.Width=w;t.Height=h;t.MipLevels=t.ArraySize=1;t.Format=DXGI_FORMAT_B8G8R8A8_UNORM;t.SampleDesc.Count=1;t.BindFlags=bind;
  if(staging){t.Usage=D3D11_USAGE_STAGING;t.CPUAccessFlags=D3D11_CPU_ACCESS_READ;}
  ComPtr<ID3D11Texture2D> tex;check(d->CreateTexture2D(&t,nullptr,&tex));return tex;
 }
 int upload(int w,int h,void* data,int stride){
  if(w<1||h<1||w>atlasSize||h>atlasSize||stride<w*4)return -1;
  if(!pages.empty()){auto& p=pages.back();if(p.x+w>atlasSize){p.x=0;p.y+=p.row;p.row=0;}}
  if(pages.empty()||pages.back().y+h>atlasSize){
   if(pages.size()>=4)return -1;
   Page page;page.tex=texture(atlasSize,atlasSize,D3D11_BIND_SHADER_RESOURCE);
   check(d->CreateShaderResourceView(page.tex.Get(),nullptr,&page.view));pages.push_back(page);
  }
  auto& p=pages.back();D3D11_BOX box={(UINT)p.x,(UINT)p.y,0,(UINT)(p.x+w),(UINT)(p.y+h),1};
  c->UpdateSubresource(p.tex.Get(),0,&box,data,stride,0);
  tiles.push_back({p.x,p.y,w,h,(int)pages.size()-1});p.x+=w;p.row=std::max(p.row,h);return (int)tiles.size()-1;
 }
 void begin(Target& t,int w,int h,const Params& params,bool effect){
  if(w<1||h<1||w>8192||h>8192)throw E_INVALIDARG;
  if(t.w!=w||t.h!=h){
   Target n;n.output=texture(w,h,D3D11_BIND_RENDER_TARGET|D3D11_BIND_SHADER_RESOURCE);
   check(d->CreateShaderResourceView(n.output.Get(),nullptr,&n.rendered));
   check(d->CreateRenderTargetView(n.output.Get(),nullptr,&n.view));
   if(&t==&fx){n.input=texture(w,h,D3D11_BIND_SHADER_RESOURCE);check(d->CreateShaderResourceView(n.input.Get(),nullptr,&n.source));}
   n.w=w;n.h=h;t=std::move(n);
  }
  c->UpdateSubresource(constants.Get(),0,nullptr,&params,0,0);c->VSSetConstantBuffers(0,1,constants.GetAddressOf());c->PSSetConstantBuffers(0,1,constants.GetAddressOf());
  c->RSSetState(raster.Get());D3D11_VIEWPORT vp={0,0,(float)w,(float)h,0,1};c->RSSetViewports(1,&vp);
  c->OMSetRenderTargets(1,t.view.GetAddressOf(),nullptr);c->OMSetBlendState(effect?nullptr:alpha.Get(),nullptr,0xffffffff);
  c->IASetPrimitiveTopology(D3D11_PRIMITIVE_TOPOLOGY_TRIANGLELIST);
  c->IASetInputLayout(effect?nullptr:layout.Get());c->VSSetShader(effect?screen.Get():vs.Get(),nullptr,0);c->PSSetShader(effect?post.Get():ps.Get(),nullptr,0);
 }
 void finish(Target& t,void* dest,int stride){
  ID3D11ShaderResourceView* none=nullptr;c->PSSetShaderResources(0,1,&none);c->OMSetRenderTargets(0,nullptr,nullptr);
  if(!t.readback)t.readback=texture(t.w,t.h,0,true);
  c->CopyResource(t.readback.Get(),t.output.Get());D3D11_MAPPED_SUBRESOURCE map={};check(c->Map(t.readback.Get(),0,D3D11_MAP_READ,0,&map));
  for(int y=0;y<t.h;y++)memcpy((BYTE*)dest+y*stride,(BYTE*)map.pData+y*map.RowPitch,t.w*4);
  c->Unmap(t.readback.Get(),0);
 }
 void draw(Quad* quads,int count,const float* m,int w,int h,void* dest,int stride){
  Params params={(float)w,(float)h};begin(scene,w,h,params,false);float clear[4]={};c->ClearRenderTargetView(scene.view.Get(),clear);
  drawSprites(quads,count,m);
  finish(scene,dest,stride);
 }
 void drawSprites(const Quad* quads,int count,const float* m){
  if(!count)return;
  stream.resize(count);runs.clear();int last=-1;
  for(int i=0;i<count;i++){
   const auto& q=quads[i];const auto& t=tiles.at(q.tile);
   if(t.page!=last){runs.push_back({(unsigned)i,t.page});last=t.page;}
   float rad=-q.angle*0.01745329252f,co=std::cos(rad),si=std::sin(rad);
   float ax=co*q.sx,ay=si*q.sx,bx=-si*q.sy,by=co*q.sy;
   float x=q.x-q.ox*ax-q.oy*bx,y=q.y-q.ox*ay-q.oy*by;
   stream[i]={x*m[0]+y*m[2]+m[4],x*m[1]+y*m[3]+m[5],
    {(ax*m[0]+ay*m[2])*t.w,(ax*m[1]+ay*m[3])*t.w},
    {(bx*m[0]+by*m[2])*t.h,(bx*m[1]+by*m[3])*t.h},
    {t.x/(float)atlasSize,t.y/(float)atlasSize,(t.x+t.w)/(float)atlasSize,(t.y+t.h)/(float)atlasSize},q.rgba};
  }
  if(stream.size()>capacity){
   capacity=std::max(stream.size(),capacity+capacity/2);D3D11_BUFFER_DESC b={};b.ByteWidth=(UINT)(capacity*sizeof(Instance));b.BindFlags=D3D11_BIND_VERTEX_BUFFER;b.Usage=D3D11_USAGE_DYNAMIC;b.CPUAccessFlags=D3D11_CPU_ACCESS_WRITE;vertices.Reset();check(d->CreateBuffer(&b,nullptr,&vertices));
  }
  D3D11_MAPPED_SUBRESOURCE map={};check(c->Map(vertices.Get(),0,D3D11_MAP_WRITE_DISCARD,0,&map));memcpy(map.pData,stream.data(),stream.size()*sizeof(Instance));c->Unmap(vertices.Get(),0);
  UINT size=sizeof(Instance),offset=0;c->IASetVertexBuffers(0,1,vertices.GetAddressOf(),&size,&offset);c->PSSetSamplers(0,1,sampler.GetAddressOf());
  for(size_t i=0;i<runs.size();i++){
   auto r=runs[i];UINT end=i+1<runs.size()?runs[i+1].first:(UINT)stream.size();
   c->PSSetShaderResources(0,1,pages[r.second].view.GetAddressOf());c->DrawInstanced(6,end-r.first,0,r.first);
  }
 }
 // The background survives between frames. Scene and post targets remain resident until
 // the last pass completes; the only synchronization with the CPU is the final Map.
 void setBase(int w,int h,void* src,int stride){
  if(!backdrop||baseW!=w||baseH!=h){backdrop=texture(w,h,0);baseW=w;baseH=h;}
  c->UpdateSubresource(backdrop.Get(),0,nullptr,src,stride,0);
 }
 void frame(Quad* q,int count,const float* matrix,int w,int h,const int* clip,
            const int* modes,const float* amounts,const int* offsets,int effects,int rw,int rh,const float* region,void* dest,int stride){
  if(!backdrop||baseW!=w||baseH!=h)throw E_INVALIDARG;
  Params params={(float)w,(float)h};begin(scene,w,h,params,false);
  c->CopyResource(scene.output.Get(),backdrop.Get());
  D3D11_RECT bounds={clip[0],clip[1],clip[2],clip[3]};
  c->RSSetState(clipped.Get());c->RSSetScissorRects(1,&bounds);
  drawSprites(q,count,matrix);
  Target* result=&scene;
  bool scaled=effects>0&&(region[0]!=0||region[1]!=0||region[2]!=w||region[3]!=h||rw!=w||rh!=h);
  if(scaled){
   float sx=region[2]/rw,sy=region[3]/rh;
   const float map[]={region[0]+.5f*(1-sx),region[1]+.5f*(1-sy),sx,sy};
   scaleTo(scene,crop,rw,rh,map,nullptr);
   result=&crop;
  }
  for(int i=0;i<effects;i++){
   Target& next=i%2?postB:postA;
   Params pass={(float)rw,(float)rh,modes[i],amounts[i]};
   for(int k=0;k<modes[i];k++){pass.offsets[k][0]=offsets[32*i+2*k];pass.offsets[k][1]=offsets[32*i+2*k+1];}
   begin(next,rw,rh,pass,true);c->PSSetShaderResources(0,1,result->rendered.GetAddressOf());
   c->Draw(3,0);result=&next;
  }
  if(scaled){
   // DrawImage(RectangleF) first stretches to an integral pixel span, then translates
   // that image. Combining the two bilinear filters would sharpen fractional pans.
   int dw=std::max(1,(int)std::round(region[2])),dh=std::max(1,(int)std::round(region[3]));
   if(dw!=rw||dh!=rh){
    const float resize[]={0,0,rw/(float)dw,rh/(float)dh};
    scaleTo(*result,stretch,dw,dh,resize,nullptr);result=&stretch;
   }
   const float map[]={-region[0],-region[1],1,1};
   D3D11_RECT area={std::max(bounds.left,(LONG)std::ceil(region[0]-.5f)),std::max(bounds.top,(LONG)std::ceil(region[1]-.5f)),
     std::min(bounds.right,(LONG)std::ceil(region[0]+region[2]-.5f)),std::min(bounds.bottom,(LONG)std::ceil(region[1]+region[3]-.5f))};
   if(area.right>area.left&&area.bottom>area.top)scaleTo(*result,scene,w,h,map,&area);
   result=&scene;
  }
  finish(*result,dest,stride);
 }
 void scaleTo(Target& src,Target& dst,int w,int h,const float* map,const D3D11_RECT* area){
  Params p={(float)w,(float)h};memcpy(p.mapping,map,sizeof(p.mapping));begin(dst,w,h,p,true);
  if(area){c->RSSetState(clipped.Get());c->RSSetScissorRects(1,area);c->OMSetBlendState(premultiplied.Get(),nullptr,0xffffffff);}
  c->PSSetShader(resample.Get(),nullptr,0);c->PSSetSamplers(1,1,linear.GetAddressOf());
  c->PSSetShaderResources(0,1,src.rendered.GetAddressOf());c->Draw(3,0);
 }
 void effect(int w,int h,void* src,int pitch,void* dest,int stride,int mode,float amount,const int* xy){
  Params params={(float)w,(float)h,mode,amount};for(int i=0;i<mode;i++){params.offsets[i][0]=xy[2*i];params.offsets[i][1]=xy[2*i+1];}
  begin(fx,w,h,params,true);c->UpdateSubresource(fx.input.Get(),0,nullptr,src,pitch,0);c->PSSetShaderResources(0,1,fx.source.GetAddressOf());c->Draw(3,0);finish(fx,dest,stride);
 }
};
extern "C" {
__declspec(dllexport) Device* rp_create(wchar_t* name,int length){try{return new Device(name,length);}catch(...){return nullptr;}}
__declspec(dllexport) void rp_release(Device* d){delete d;}
__declspec(dllexport) int rp_upload(Device* d,int w,int h,void* data,int stride){try{return d->upload(w,h,data,stride);}catch(...){return -1;}}
__declspec(dllexport) int rp_draw(Device* d,Quad* q,int count,float* matrix,int w,int h,void* dest,int stride){try{d->draw(q,count,matrix,w,h,dest,stride);return 1;}catch(...){return 0;}}
__declspec(dllexport) int rp_base(Device* d,int w,int h,void* data,int stride){try{d->setBase(w,h,data,stride);return 1;}catch(...){return 0;}}
__declspec(dllexport) int rp_frame(Device* d,Quad* q,int count,float* m,int w,int h,int* clip,int* modes,float* amounts,int* offsets,int effects,int rw,int rh,float* region,void* dest,int stride){
 try{d->frame(q,count,m,w,h,clip,modes,amounts,offsets,effects,rw,rh,region,dest,stride);return 1;}catch(...){return 0;}
}
__declspec(dllexport) int rp_effect(Device* d,int w,int h,void* src,int pitch,void* dest,int stride,int mode,float opacity,int* xy){try{d->effect(w,h,src,pitch,dest,stride,mode,opacity,xy);return 1;}catch(...){return 0;}}
}
