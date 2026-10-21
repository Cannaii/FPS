using System;
using AFPS.Math;
using BigInteger = System.Numerics.BigInteger;

namespace AFPS.Simulation.World
{
    /// <summary>胶囊对三角形等价为球对沿 Y 负向拉伸的三棱柱；连续检查面、边和顶点。</summary>
    internal readonly struct TriangleCapsuleSweep
    {
        private static readonly int[][] Faces={new[]{0,1,2},new[]{3,5,4},new[]{0,3,4,1},new[]{1,4,5,2},new[]{2,5,3,0}};
        private static readonly int[,] Edges={{0,1},{1,2},{2,0},{3,4},{4,5},{5,3},{0,3},{1,4},{2,5}};
        private readonly WideVector a,b,c,d,e,f,start,velocity;
        private readonly WideVector topNormal,sideAB,sideBC,sideCA;
        private readonly BigInteger radius;
        private readonly bool hasVolume;

        public TriangleCapsuleSweep(in FixedWorldTriangle triangle, FixVec3 feet, Fix radius, Fix height, FixVec3 direction)
        {
            a=new WideVector(triangle.A)*Fix.Scale;
            b=new WideVector(triangle.B)*Fix.Scale;
            c=new WideVector(triangle.C)*Fix.Scale;
            WideVector n=WideVector.Cross(b-a,c-a);
            if(n.Y.Sign<0) { WideVector swap=b; b=c; c=swap; }
            long segment=(long)height.Raw-2L*radius.Raw;
            WideVector offset=new WideVector(0,segment*Fix.Scale,0);
            d=a-offset; e=b-offset; f=c-offset;
            // 法线只需要方向：约去所有公共整数比例，避免把坐标缩放因子反复带入宽乘积。
            topNormal=WideVector.Cross(b-a,c-a).Primitive();
            sideAB=WideVector.Cross(d-a,e-a).Primitive();
            sideBC=WideVector.Cross(e-b,f-b).Primitive();
            sideCA=WideVector.Cross(f-c,d-c).Primitive();
            hasVolume=segment>0 && !n.Y.IsZero;
            start=(new WideVector(feet)+new WideVector(0,radius.Raw,0))*Fix.Scale;
            velocity=new WideVector(direction);
            this.radius=(BigInteger)radius.Raw*Fix.Scale;
        }

        private WideVector Vertex(int i) => i==0 ? a : i==1 ? b : i==2 ? c : i==3 ? d : i==4 ? e : f;
        private WideVector FaceNormal(int[] face) => face==Faces[0] ? topNormal : face==Faces[1] ? -topNormal : face==Faces[2] ? sideAB : face==Faces[3] ? sideBC : sideCA;
        private bool ContainsProjection(WideVector point,int[] face,WideVector normal)
        {
            for(int i=0;i<face.Length;i++)
            {
                WideVector from=Vertex(face[i]),edge=Vertex(face[(i+1)%face.Length])-from;
                if(WideVector.Dot(WideVector.Cross(edge,point-from),normal).Sign<0) return false;
            }
            return true;
        }
        private bool ContainsProjection(CapsuleSweepTime time,int[] face,WideVector normal)
        {
            for(int i=0;i<face.Length;i++)
            {
                WideVector from=Vertex(face[i]),edge=Vertex(face[(i+1)%face.Length])-from;
                BigInteger constant=WideVector.Dot(WideVector.Cross(edge,start-from),normal);
                BigInteger slope=WideVector.Dot(WideVector.Cross(edge,velocity),normal);
                if(time.SignOfLinear(constant,slope)<0) return false;
            }
            return true;
        }

        public bool Cast(int maximum,out CapsuleSweepTime time,out bool inside,out FixVec3 normal,out FixVec3 point)
        {
            time=default; inside=false; normal=default; point=default;
            // 胶囊球心若在某个支撑平面外且继续远离，就不可能进入棱柱的球膨胀体。
            // 等号对应纯切触；这也快速排除在平地上横向移动时所有共面三角形。
            foreach(int[] face in Faces)
            {
                WideVector n=FaceNormal(face);
                BigInteger squared=WideVector.Dot(n,n);
                if(squared.IsZero) continue;
                BigInteger signed=WideVector.Dot(start-Vertex(face[0]),n);
                if(signed.Sign>0 && WideVector.Dot(velocity,n).Sign>=0 && signed*signed>=radius*radius*squared) return false;
            }
            Closest closest=GetClosest(start,out bool centerInside);
            if(centerInside || closest.SquaredNumerator<=radius*radius*closest.SquaredDenominator)
            {
                WideVector escape=closest.Vector.IsZero ? closest.Fallback : closest.Vector;
                if(centerInside) escape=-escape;
                if(WideVector.Dot(velocity,escape).Sign>=0) return false;
                time=CapsuleSweepTime.Rational(0,1);
                inside=centerInside || closest.SquaredNumerator<radius*radius*closest.SquaredDenominator;
                normal=escape.Normalized(); point=OriginalPoint(closest);
                return true;
            }
            // 排除二次方程重根：纯切触不进入表面膨胀体，不能消耗共面内边的滑动迭代。
            bool found=false;
            foreach(int[] face in Faces)
            {
                WideVector n=FaceNormal(face);
                BigInteger squared=WideVector.Dot(n,n);
                if(squared.IsZero) continue;
                BigInteger p=WideVector.Dot(start-Vertex(face[0]),n),v=WideVector.Dot(velocity,n);
                if(!CapsuleSweepTime.TryQuadratic(v*v,2*p*v,p*p-radius*radius*squared,maximum,out CapsuleSweepTime candidate,rejectTangent:true) || !ContainsProjection(candidate,face,n)) continue;
                Consider(candidate,ref time,ref found);
            }
            for(int i=0;i<9;i++)
            {
                WideVector from=Vertex(Edges[i,0]),edge=(Vertex(Edges[i,1])-from).Primitive(out BigInteger lengthScale),delta=start-from;
                BigInteger squared=WideVector.Dot(edge,edge);
                if(squared.IsZero) continue;
                BigInteger p=WideVector.Dot(delta,edge),v=WideVector.Dot(velocity,edge);
                BigInteger qa=WideVector.Dot(velocity,velocity)*squared-v*v;
                BigInteger qb=2*(WideVector.Dot(delta,velocity)*squared-p*v);
                BigInteger qc=(WideVector.Dot(delta,delta)-radius*radius)*squared-p*p;
                if(!CapsuleSweepTime.TryQuadratic(qa,qb,qc,maximum,out CapsuleSweepTime candidate,rejectTangent:true)) continue;
                if(candidate.SignOfLinear(p,v)<0 || candidate.SignOfLinear(squared*lengthScale-p,-v)<0) continue;
                Consider(candidate,ref time,ref found);
            }
            for(int i=0;i<6;i++)
            {
                WideVector delta=start-Vertex(i);
                if(CapsuleSweepTime.TryQuadratic(WideVector.Dot(velocity,velocity),2*WideVector.Dot(delta,velocity),WideVector.Dot(delta,delta)-radius*radius,maximum,out CapsuleSweepTime candidate,rejectTangent:true)) Consider(candidate,ref time,ref found);
            }
            if(!found) return false;
            closest=GetClosest(start+velocity*time.FloorRaw,out _);
            normal=(closest.Vector.IsZero ? closest.Fallback : closest.Vector).Normalized();
            point=OriginalPoint(closest);
            return true;
        }
        private static void Consider(CapsuleSweepTime candidate,ref CapsuleSweepTime time,ref bool found)
        {
            if(found && candidate.CompareTo(time)>=0) return;
            found=true; time=candidate;
        }

        private struct Closest
        {
            public BigInteger SquaredNumerator,SquaredDenominator,PointDenominator;
            public WideVector Vector,PointNumerator,Fallback;
            public void Consider(BigInteger squaredNumerator,BigInteger squaredDenominator,WideVector vector,WideVector pointNumerator,BigInteger pointDenominator,WideVector fallback)
            {
                if(!SquaredDenominator.IsZero && squaredNumerator*SquaredDenominator>=SquaredNumerator*squaredDenominator) return;
                SquaredNumerator=squaredNumerator; SquaredDenominator=squaredDenominator;
                Vector=vector; PointNumerator=pointNumerator; PointDenominator=pointDenominator; Fallback=fallback;
            }
        }
        private Closest GetClosest(WideVector position,out bool inside)
        {
            Closest closest=default;
            inside=hasVolume;
            foreach(int[] face in Faces)
            {
                WideVector n=FaceNormal(face);
                BigInteger squared=WideVector.Dot(n,n);
                if(squared.IsZero) continue;
                BigInteger signed=WideVector.Dot(position-Vertex(face[0]),n);
                if(signed.Sign>=0) inside=false;
                if(ContainsProjection(position,face,n))
                {
                    closest.Consider(signed*signed,squared,n*signed,position*squared-n*signed,squared,n);
                    // 点在凸体支撑面外、正交投影位于面内时，它就是整个凸体的最近点。
                    if(signed.Sign>=0) {inside=false;return closest;}
                }
            }
            for(int i=0;i<9;i++)
            {
                WideVector from=Vertex(Edges[i,0]),edge=(Vertex(Edges[i,1])-from).Primitive(out BigInteger lengthScale),delta=position-from;
                BigInteger squared=WideVector.Dot(edge,edge);
                if(squared.IsZero) continue;
                BigInteger projection=BigInteger.Max(0,BigInteger.Min(squared*lengthScale,WideVector.Dot(delta,edge)));
                WideVector vector=delta*squared-edge*projection;
                closest.Consider(WideVector.Dot(vector,vector),squared*squared,vector,from*squared+edge*projection,squared,FaceNormal(Faces[0]));
            }
            return closest;
        }

        // 将拉伸棱柱上的接触点沿 Y 映射回原三角形。垂直三角形取同一竖线与三角形的交区间。
        private FixVec3 OriginalPoint(Closest closest)
        {
            WideVector q=closest.PointNumerator,n=topNormal;
            BigInteger den=closest.PointDenominator;
            if(!n.Y.IsZero)
            {
                BigInteger y=a.Y*den*n.Y-n.X*(q.X-a.X*den)-n.Z*(q.Z-a.Z*den);
                return new WideVector(q.X*n.Y,y,q.Z*n.Y).ToPoint(den*n.Y*Fix.Scale);
            }
            bool useX=BigInteger.Abs(n.Z)>=BigInteger.Abs(n.X);
            BigInteger horizontal=useX ? q.X : q.Z;
            BigInteger minN=0,minD=0,maxN=0,maxD=0;
            for(int i=0;i<3;i++)
            {
                WideVector p=Vertex(i),r=Vertex((i+1)%3);
                BigInteger h0=useX ? p.X : p.Z,h1=useX ? r.X : r.Z;
                if(horizontal<BigInteger.Min(h0,h1)*den || horizontal>BigInteger.Max(h0,h1)*den) continue;
                if(h0==h1) { AddHeight(p.Y,1,ref minN,ref minD,ref maxN,ref maxD); AddHeight(r.Y,1,ref minN,ref minD,ref maxN,ref maxD); }
                else AddHeight(p.Y*(h1-h0)*den+(horizontal-h0*den)*(r.Y-p.Y),(h1-h0)*den,ref minN,ref minD,ref maxN,ref maxD);
            }
            BigInteger yN=q.Y,yD=den;
            if(!minD.IsZero && yN*minD<minN*yD) {yN=minN;yD=minD;}
            if(!maxD.IsZero && yN*maxD>maxN*yD) {yN=maxN;yD=maxD;}
            return new WideVector(q.X*yD,yN*den,q.Z*yD).ToPoint(den*yD*Fix.Scale);
        }
        private static void AddHeight(BigInteger n,BigInteger d,ref BigInteger minN,ref BigInteger minD,ref BigInteger maxN,ref BigInteger maxD)
        {
            if(d.Sign<0) {n=-n;d=-d;}
            if(minD.IsZero || n*minD<minN*d) {minN=n;minD=d;}
            if(maxD.IsZero || n*maxD>maxN*d) {maxN=n;maxD=d;}
        }
    }
}
