using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows.Media;
using InkCanvasForClassX.Libraries.Stroke;
using Jint;
using Jint.Native;

namespace InkCanvasForClassX.Libraries
{
    public class PerfectFreehandJint {
        public class  StylusPointLite
        {
            public double x;
            public double y;
            public double pressure;
        }

        public class StrokeOptions
        {
            public double? size { get; set; }
            public double? thinning { get; set; }
            public double? smoothing { get; set; }
            public double? streamline { get; set; }
            public Func<double, double> easing { get; set; }
            public bool? simulatePressure { get; set; }
            public StrokeCapOptions start { get; set; }
            public StrokeCapOptions end { get; set; }
            public bool? last { get; set; }
        }

        public class StrokeCapOptions
        {
            public bool? cap { get; set; }
            public double? taper { get; set; }
            public Func<double, double> easing { get; set; }
        }

        public class StrokePoint
        {
            public Array point { get; set; }
            public double pressure { get; set; }
            public double distance { get; set; }
            public Array vector { get; set; }
            public double runningLength { get; set; }
        }

        public readonly Engine JintEngine = new Engine();

        public PerfectFreehandJint() {
            // perfect-freehand
            JintEngine.Execute("\"use strict\";function neg(t){return[-t[0],-t[1]]}function add(t,n){return[t[0]+n[0],t[1]+n[1]]}function sub(t,n){return[t[0]-n[0],t[1]-n[1]]}function mul(t,n){return[t[0]*n,t[1]*n]}function div(t,n){return[t[0]/n,t[1]/n]}function per(t){return[t[1],-t[0]]}function dpr(t,n){return t[0]*n[0]+t[1]*n[1]}function isEqual(t,n){return t[0]===n[0]&&t[1]===n[1]}function len(t){return Math.hypot(t[0],t[1])}function len2(t){return t[0]*t[0]+t[1]*t[1]}function dist2(t,n){return len2(sub(t,n))}function uni(t){return div(t,len(t))}function dist(t,n){return Math.hypot(t[1]-n[1],t[0]-n[0])}function med(t,n){return mul(add(t,n),.5)}function rotAround(t,n,e){const r=Math.sin(e),u=Math.cos(e),i=t[0]-n[0],s=t[1]-n[1],o=i*r+s*u;return[i*u-s*r+n[0],o+n[1]]}function lrp(t,n,e){return add(t,mul(sub(n,t),e))}function prj(t,n,e){return add(t,mul(n,e))}function getStrokeRadius(t,n,e,r=(t=>t)){return t*r(.5-n*(.5-e))}function getStrokePoints(t,n={}){var e;const{streamline:r=.5,size:u=16,last:i=!1}=n;if(0===t.length)return[];const s=.15+.85*(1-r);let o=Array.isArray(t[0])?t:t.map((({x:t,y:n,pressure:e=.5})=>[t,n,e]));if(2===o.length){const t=o[1];o=o.slice(0,-1);for(let n=1;n<5;n++)o.push(lrp(o[0],t,n/4))}1===o.length&&(o=[...o,[...add(o[0],[1,1]),...o[0].slice(2)]]);const c=[{point:[o[0][0],o[0][1]],pressure:o[0][2]>=0?o[0][2]:.25,vector:[1,1],distance:0,runningLength:0}];let l=!1,p=0,a=c[0];const d=o.length-1;for(let t=1;t<o.length;t++){const n=i&&t===d?o[t].slice(0,2):lrp(a.point,o[t],s);if(isEqual(a.point,n))continue;const e=dist(n,a.point);if(p+=e,t<d&&!l){if(p<u)continue;l=!0}a={point:n,pressure:o[t][2]>=0?o[t][2]:.5,vector:uni(sub(a.point,n)),distance:e,runningLength:p},c.push(a)}return c[0].vector=(null===(e=c[1])||void 0===e?void 0:e.vector)||[0,0],c}const{min:min,PI:PI}=Math,RATE_OF_PRESSURE_CHANGE=.275,FIXED_PI=PI+1e-4;function getStrokeOutlinePoints(t,n={}){const{size:e=16,smoothing:r=.5,thinning:u=.5,simulatePressure:i=!0,easing:s=(t=>t),start:o={},end:c={},last:l=!1}=n,{cap:p=!0,easing:a=(t=>t*(2-t))}=o,{cap:d=!0,easing:h=(t=>--t*t*t+1)}=c;if(0===t.length||e<=0)return[];const f=t[t.length-1].runningLength,g=!1===o.taper?0:!0===o.taper?Math.max(e,f):o.taper,m=!1===c.taper?0:!0===c.taper?Math.max(e,f):c.taper,E=Math.pow(e*r,2),P=[],v=[];let I,_=t.slice(0,10).reduce(((t,n)=>{let r=n.pressure;if(i){const u=min(1,n.distance/e),i=min(1,1-u);r=min(1,t+u*RATE_OF_PRESSURE_CHANGE*(i-t))}return(t+r)/2}),t[0].pressure),A=getStrokeRadius(e,u,t[t.length-1].pressure,s),S=t[0].vector,b=t[0].point,R=b,M=b,F=R,k=!1;for(let n=0;n<t.length;n++){let{pressure:r}=t[n];const{point:o,vector:c,distance:l,runningLength:p}=t[n];if(n<t.length-1&&f-p<3)continue;if(u){if(i){const t=min(1,l/e),n=min(1,1-t);r=min(1,_+t*RATE_OF_PRESSURE_CHANGE*(n-_))}A=getStrokeRadius(e,u,r,s)}else A=e/2;void 0===I&&(I=A);const d=p<g?a(p/g):1,D=f-p<m?h((f-p)/m):1;A=Math.max(.01,A*Math.min(d,D));const X=(n<t.length-1?t[n+1]:t[n]).vector,y=n<t.length-1?dpr(c,X):1,O=null!==y&&y<0;if(dpr(c,S)<0&&!k||O){const t=mul(per(S),A);for(let n=1/13,e=0;e<=1;e+=n)M=rotAround(sub(o,t),o,FIXED_PI*e),P.push(M),F=rotAround(add(o,t),o,FIXED_PI*-e),v.push(F);b=M,R=F,O&&(k=!0);continue}if(k=!1,n===t.length-1){const t=mul(per(c),A);P.push(sub(o,t)),v.push(add(o,t));continue}const x=mul(per(lrp(X,c,y)),A);M=sub(o,x),(n<=1||dist2(b,M)>E)&&(P.push(M),b=M),F=add(o,x),(n<=1||dist2(R,F)>E)&&(v.push(F),R=F),_=r,S=c}const D=t[0].point.slice(0,2),X=t.length>1?t[t.length-1].point.slice(0,2):add(t[0].point,[1,1]),y=[],O=[];if(1===t.length){if(!g&&!m||l){const t=prj(D,uni(per(sub(D,X))),-(I||A)),n=[];for(let e=1/13,r=e;r<=1;r+=e)n.push(rotAround(t,D,2*FIXED_PI*r));return n}}else{if(g||m&&1===t.length);else if(p)for(let t=1/13,n=t;n<=1;n+=t){const t=rotAround(v[0],D,FIXED_PI*n);y.push(t)}else{const t=sub(P[0],v[0]),n=mul(t,.5),e=mul(t,.51);y.push(sub(D,n),sub(D,e),add(D,e),add(D,n))}const n=per(neg(t[t.length-1].vector));if(m||g&&1===t.length)O.push(X);else if(d){const t=prj(X,n,A);for(let n=1/29,e=n;e<1;e+=n)O.push(rotAround(t,X,3*FIXED_PI*e))}else O.push(add(X,mul(n,A)),add(X,mul(n,.99*A)),sub(X,mul(n,.99*A)),sub(X,mul(n,A)))}return P.concat(O,v.reverse(),y)}function getStroke(t,n={}){return getStrokeOutlinePoints(getStrokePoints(t,n),n)}");
            // getSvgPathFromStroke
            JintEngine.Execute(
                "\"use strict\";const average=(e,t)=>(e+t)/2;function getSvgPathFromStroke(e,t=!0){const o=e.length;if(o<4)return\"\";let r=e[0],a=e[1];const i=e[2];let F=`M${r[0].toFixed(2)},${r[1].toFixed(2)} Q${a[0].toFixed(2)},${a[1].toFixed(2)} ${average(a[0],i[0]).toFixed(2)},${average(a[1],i[1]).toFixed(2)} T`;for(let t=2,i=o-1;t<i;t++)r=e[t],a=e[t+1],F+=`${average(r[0],a[0]).toFixed(2)},${average(r[1],a[1]).toFixed(2)} `;return t&&(F+=\"Z\"),F}");
            // getSvgPathStroke
            JintEngine.Execute("function getSvgPathStroke(points,options,closed=true){return getSvgPathFromStroke(getStroke(points,options),closed)}");
        }

        public JsArray GetStroke(StylusPointLite[] points, StrokeOptions options ) {
            return (JsArray)JintEngine.Invoke("getStrokePoints", points, options);
        }

        public string GetSVGPathStroke(StylusPointLite[] points, StrokeOptions options, bool closed = true) {
            return ((JsString)JintEngine.Invoke("getSvgPathStroke", points, options, true)).ToString();
        }

        public Geometry GetGeometryStroke(StylusPointLite[] points, StrokeOptions options, bool closed = true) {
            return Geometry.Parse("F1 "+((JsString)JintEngine.Invoke("getSvgPathStroke", points, options, true)).ToString());
        }
    }
}
