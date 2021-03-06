#region Copyright Notice

// Copyright (c) by Achilles Software, All rights reserved.
//
// Licensed under the MIT License. See License.txt in the project root for license information.
//
// Send questions regarding this copyright notice to: mailto:todd.thomson@achilles-software.com

/*
Microsoft Automatic Graph Layout,MSAGL 

Copyright (c) Microsoft Corporation

All rights reserved. 

MIT License 

Permission is hereby granted, free of charge, to any person obtaining
a copy of this software and associated documentation files (the
""Software""), to deal in the Software without restriction, including
without limitation the rights to use, copy, modify, merge, publish,
distribute, sublicense, and/or sell copies of the Software, and to
permit persons to whom the Software is furnished to do so, subject to
the following conditions:

The above copyright notice and this permission notice shall be
included in all copies or substantial portions of the Software.

THE SOFTWARE IS PROVIDED *AS IS*, WITHOUT WARRANTY OF ANY KIND,
EXPRESS OR IMPLIED, INCLUDING BUT NOT LIMITED TO THE WARRANTIES OF
MERCHANTABILITY, FITNESS FOR A PARTICULAR PURPOSE AND
NONINFRINGEMENT. IN NO EVENT SHALL THE AUTHORS OR COPYRIGHT HOLDERS BE
LIABLE FOR ANY CLAIM, DAMAGES OR OTHER LIABILITY, WHETHER IN AN ACTION
OF CONTRACT, TORT OR OTHERWISE, ARISING FROM, OUT OF OR IN CONNECTION
WITH THE SOFTWARE OR THE USE OR OTHER DEALINGS IN THE SOFTWARE.
*/

#endregion

#region Namespaces

using Microsoft.Msagl.Core.Geometry.Curves;
using Microsoft.Msagl.Core.Layout;
using Microsoft.Msagl.Layout.LargeGraphLayout;
using Msagl.Uwp.UI.Controls;
using Msagl.Uwp.UI.Layout;

using System;
using System.Collections.Generic;

using Windows.UI;
using Windows.UI.Xaml;
using Windows.UI.Xaml.Controls;
using Windows.UI.Xaml.Media;
using Windows.UI.Xaml.Shapes;

using Edge = Msagl.Uwp.UI.Layout.Edge;
using Ellipse = Microsoft.Msagl.Core.Geometry.Curves.Ellipse;
using LineSegment = Microsoft.Msagl.Core.Geometry.Curves.LineSegment;
using Point = Microsoft.Msagl.Core.Geometry.Point;
using Polyline = Microsoft.Msagl.Core.Geometry.Curves.Polyline;
using Rectangle = Microsoft.Msagl.Core.Geometry.Rectangle;
using Size = Windows.Foundation.Size;

#endregion

namespace Msagl.Uwp.UI
{
    internal class VEdge : IViewerEdge, IInvalidatable
    {
        #region events

        public event EventHandler MarkedForDraggingEvent;
        public event EventHandler UnmarkedForDraggingEvent;

        #endregion

        #region Fields

        internal FrameworkElement LabelFrameworkElement;

        #endregion

        #region Constructor(s)

        public VEdge( Edge edge, FrameworkElement labelFrameworkElement, Func<double> pathStrokeThicknessFunc )
        {
            Edge = edge;

            PathStrokeThicknessFunc = pathStrokeThicknessFunc;

            CurvePath = new Path
            {
                Data = GetICurveGeometry( edge.GeometryEdge.Curve ),
                Tag = this
            };

            EdgeAttrClone = edge.Attr.Clone();

            if ( edge.Attr.ArrowAtSource )
                SourceArrowHeadPath = new Path
                {
                    Data = DefiningSourceArrowHead(),
                    Tag = this
                };

            if ( edge.Attr.ArrowAtTarget )
                TargetArrowHeadPath = new Path
                {
                    Data = DefiningTargetArrowHead( Edge.GeometryEdge.EdgeGeometry, PathStrokeThickness ),
                    Tag = this
                };

            SetPathStroke();

            if ( labelFrameworkElement != null )
            {
                LabelFrameworkElement = labelFrameworkElement;
                Common.PositionFrameworkElement( LabelFrameworkElement, edge.Label.Center, 1 );
            }
            edge.Attr.VisualsChanged += ( sender, args ) => Invalidate();

            edge.IsVisibleChanged += obj =>
            {
                foreach ( var frameworkElement in FrameworkElements )
                {
                    frameworkElement.Visibility = edge.IsVisible ? Visibility.Visible : Visibility.Collapsed;
                }
            };
        }

        public VEdge( Edge edge, LgLayoutSettings lgSettings )
        {
            Edge = edge;
            EdgeAttrClone = edge.Attr.Clone();
        }

        #endregion

        internal IEnumerable<FrameworkElement> FrameworkElements
        {
            get
            {
                if ( SourceArrowHeadPath != null )
                    yield return this.SourceArrowHeadPath;

                if ( TargetArrowHeadPath != null )
                    yield return TargetArrowHeadPath;

                if ( CurvePath != null )
                    yield return CurvePath;

                if ( LabelFrameworkElement != null )
                    yield return LabelFrameworkElement;
            }
        }

        internal EdgeAttr EdgeAttrClone { get; set; }

        internal static Geometry DefiningTargetArrowHead( EdgeGeometry edgeGeometry, double thickness )
        {
            if ( edgeGeometry.TargetArrowhead == null || edgeGeometry.Curve == null )
                return null;

            var pathGeometry = new PathGeometry();
            pathGeometry.Figures = new PathFigureCollection();

            AddArrow( pathGeometry, edgeGeometry.Curve.End, edgeGeometry.TargetArrowhead.TipPosition, thickness );

            return pathGeometry;
        }

        Geometry DefiningSourceArrowHead()
        {
            var pathGeometry = new PathGeometry()
            {
                Figures = new PathFigureCollection()
            };

            var arrowFigure = new PathFigure();

            AddArrow( pathGeometry, Edge.GeometryEdge.Curve.Start, Edge.GeometryEdge.EdgeGeometry.SourceArrowhead.TipPosition, PathStrokeThickness );

            return pathGeometry;
        }

        double PathStrokeThickness
        {
            get
            {
                return PathStrokeThicknessFunc != null ? PathStrokeThicknessFunc() : this.Edge.Attr.LineWidth;
            }
        }

        internal Path CurvePath { get; set; }
        internal Path SourceArrowHeadPath { get; set; }
        internal Path TargetArrowHeadPath { get; set; }

        static internal Geometry GetICurveGeometry( ICurve curve )
        {
            var pathGeometry = new PathGeometry();

            FillPathGeometry( pathGeometry, curve );

            return pathGeometry;
        }

        static void FillPathGeometry( PathGeometry path, ICurve curve )
        {
            if ( curve == null )
                return;

            FillContextForICurve( path, curve );
        }

        static internal void FillContextForICurve( PathGeometry path, ICurve iCurve )
        {
            var curveFigure = new PathFigure()
            {
                StartPoint = Common.UwpPoint( iCurve.Start )
            };

            var c = iCurve as Curve;

            if ( c != null )
            {
                FillContexForCurve( path, c );
            }
            else
            {
                var cubicBezierSeg = iCurve as CubicBezierSegment;

                if ( cubicBezierSeg != null )
                {
                    curveFigure.Segments.Add( new BezierSegment()
                    {
                        Point1 = Common.UwpPoint( cubicBezierSeg.B( 1 ) ),
                        Point2 = Common.UwpPoint( cubicBezierSeg.B( 2 ) ),
                        Point3 = Common.UwpPoint( cubicBezierSeg.B( 3 ) )
                    } );
                }
                else
                {
                    var ls = iCurve as LineSegment;

                    if ( ls != null )
                    {
                        curveFigure.Segments.Add( new Windows.UI.Xaml.Media.LineSegment()
                        {
                            Point = Common.UwpPoint( ls.End )
                        } );
                    }
                    else
                    {
                        var rr = iCurve as RoundedRect;

                        if ( rr != null )
                        {
                            FillContexForCurve( path, rr.Curve );
                        }
                        else
                        {
                            var poly = iCurve as Polyline;

                            if ( poly != null )
                            {
                                FillContexForPolyline( path, poly );
                            }
                            else
                            {
                                var ellipse = iCurve as Ellipse;

                                if ( ellipse != null )
                                {
                                    // context.LineTo(Common.WpfPoint(ellipse.End),true,false);
                                    double sweepAngle = EllipseSweepAngle( ellipse );
                                    bool largeArc = Math.Abs( sweepAngle ) >= Math.PI;
                                    Rectangle box = ellipse.FullBox();

                                    curveFigure.Segments.Add( new ArcSegment()
                                    {
                                        Point = Common.UwpPoint( ellipse.End ),
                                        Size = new Size( box.Width / 2, box.Height / 2 ),
                                        RotationAngle = sweepAngle,
                                        IsLargeArc = largeArc,
                                        SweepDirection = sweepAngle < 0 ? SweepDirection.Counterclockwise : SweepDirection.Clockwise,
                                    } );
                                }
                                else
                                {
                                    throw new NotImplementedException();
                                }
                            }
                        }
                    }
                }
            }

            path.Figures.Add( curveFigure );
        }

        static void FillContexForPolyline( PathGeometry path, Polyline poly )
        {
            PathFigure polylineFigure = new PathFigure();

            for ( PolylinePoint pp = poly.StartPoint.Next; pp != null; pp = pp.Next )
            {
                polylineFigure.Segments.Add( new Windows.UI.Xaml.Media.LineSegment() { Point = Common.UwpPoint( pp.Point ) } );
            }

            path.Figures.Add( polylineFigure );
        }

        static void FillContexForCurve( PathGeometry path, Curve c )
        {
            PathFigure curveFigure = new PathFigure();

            foreach ( ICurve seg in c.Segments )
            {
                var bezSeg = seg as CubicBezierSegment;

                if ( bezSeg != null )
                {
                    curveFigure.Segments.Add( new BezierSegment()
                    {
                        Point1 = Common.UwpPoint( bezSeg.B( 1 ) ),
                        Point2 = Common.UwpPoint( bezSeg.B( 2 ) ),
                        Point3 = Common.UwpPoint( bezSeg.B( 3 ) )
                    } );
                }
                else
                {
                    var ls = seg as LineSegment;

                    if ( ls != null )
                    {
                        curveFigure.Segments.Add( new Windows.UI.Xaml.Media.LineSegment() { Point = Common.UwpPoint( ls.End ) } );
                    }
                    else
                    {
                        var ellipse = seg as Ellipse;

                        if ( ellipse != null )
                        {
                            // context.LineTo(Common.WpfPoint(ellipse.End),true,false);
                            double sweepAngle = EllipseSweepAngle( ellipse );
                            bool largeArc = Math.Abs( sweepAngle ) >= Math.PI;
                            Rectangle box = ellipse.FullBox();

                            curveFigure.Segments.Add( new ArcSegment()
                            {
                                Point = Common.UwpPoint( ellipse.End ),
                                Size = new Size( box.Width / 2, box.Height / 2 ),
                                RotationAngle = sweepAngle,
                                IsLargeArc = largeArc,
                                SweepDirection = sweepAngle < 0 ? SweepDirection.Counterclockwise : SweepDirection.Clockwise
                            } );
                        }
                        else
                            throw new NotImplementedException();
                    }
                }
            }

            path.Figures.Add( curveFigure );
        }

        public static double EllipseSweepAngle( Ellipse ellipse )
        {
            double sweepAngle = ellipse.ParEnd - ellipse.ParStart;

            return ellipse.OrientedCounterclockwise() ? sweepAngle : -sweepAngle;
        }

        static void AddArrow( PathGeometry path, Point start, Point end, double thickness )
        {
            PathFigure arrow = new PathFigure();

            if ( thickness > 1 )
            {
                Point dir = end - start;
                Point h = dir;
                double dl = dir.Length;

                if ( dl < 0.001 )
                    return;

                dir /= dl;

                var s = new Point( -dir.Y, dir.X );
                double w = 0.5 * thickness;
                Point s0 = w * s;

                s *= h.Length * HalfArrowAngleTan;
                s += s0;

                double rad = w / HalfArrowAngleCos;

                arrow.StartPoint = Common.UwpPoint( start + s );
                arrow.Segments.Add( new Windows.UI.Xaml.Media.LineSegment() { Point = Common.UwpPoint( start - s ) } );
                arrow.Segments.Add( new Windows.UI.Xaml.Media.LineSegment() { Point = Common.UwpPoint( end - s0 ) } );
                arrow.Segments.Add( new ArcSegment()
                {
                    Point = Common.UwpPoint( end + s0 ),
                    Size = new Size( rad, rad ),
                    RotationAngle = Math.PI - ArrowAngle,
                    IsLargeArc = false,
                    SweepDirection = SweepDirection.Clockwise
                } );
            }
            else
            {
                Point dir = end - start;
                double dl = dir.Length;
                //take into account the widths
                double delta = Math.Min( dl / 2, thickness + thickness / 2 );
                dir *= (dl - delta) / dl; 
                end = start + dir;
                dir = dir.Rotate( Math.PI / 2 );
                Point s = dir * HalfArrowAngleTan;

                arrow.StartPoint = Common.UwpPoint( start + s );
                arrow.Segments.Add( new Windows.UI.Xaml.Media.LineSegment() { Point = Common.UwpPoint( end ) } );
                arrow.Segments.Add( new Windows.UI.Xaml.Media.LineSegment() { Point = Common.UwpPoint( start - s ) } );
            }

            path.Figures.Add( arrow );
        }

        static readonly double HalfArrowAngleTan = Math.Tan( ArrowAngle * 0.5 * Math.PI / 180.0 );
        static readonly double HalfArrowAngleCos = Math.Cos( ArrowAngle * 0.5 * Math.PI / 180.0 );
        const double ArrowAngle = 30.0; //degrees

        #region Implementation of IViewerObject

        public DrawingObject DrawingObject
        {
            get { return Edge; }
        }

        public bool MarkedForDragging { get; set; }


        #endregion

        #region Implementation of IViewerEdge

        public Edge Edge { get; private set; }
        public IViewerNode Source { get; private set; }
        public IViewerNode Target { get; private set; }
        public double RadiusOfPolylineCorner { get; set; }

        public VLabel VLabel { get; set; }

        #endregion

        internal void Invalidate( FrameworkElement fe, Rail rail, byte edgeTransparency )
        {
            var path = fe as Path;
            if ( path != null )
                SetPathStrokeToRailPath( rail, path, edgeTransparency );
        }
        public void Invalidate()
        {
            var vis = Edge.IsVisible ? Visibility.Visible : Visibility.Collapsed;

            foreach ( var fe in FrameworkElements )
                fe.Visibility = vis;

            if ( vis == Visibility.Collapsed )
                return;

            CurvePath.Data = GetICurveGeometry( Edge.GeometryEdge.Curve );

            if ( Edge.Attr.ArrowAtSource )
                SourceArrowHeadPath.Data = DefiningSourceArrowHead();

            if ( Edge.Attr.ArrowAtTarget )
                TargetArrowHeadPath.Data = DefiningTargetArrowHead( Edge.GeometryEdge.EdgeGeometry, PathStrokeThickness );

            SetPathStroke();

            if ( VLabel != null )
                ((IInvalidatable)VLabel).Invalidate();
        }

        void SetPathStroke()
        {
            SetPathStrokeToPath( CurvePath );

            if ( SourceArrowHeadPath != null )
            {
                SourceArrowHeadPath.Stroke = SourceArrowHeadPath.Fill = new SolidColorBrush( Edge.Attr.Color );
                SourceArrowHeadPath.StrokeThickness = PathStrokeThickness;
            }
            if ( TargetArrowHeadPath != null )
            {
                TargetArrowHeadPath.Stroke = TargetArrowHeadPath.Fill = new SolidColorBrush( Edge.Attr.Color );
                TargetArrowHeadPath.StrokeThickness = PathStrokeThickness;
            }
        }

        void SetPathStrokeToRailPath( Rail rail, Path path, byte transparency )
        {

            path.Stroke = SetStrokeColorForRail( transparency, rail );
            path.StrokeThickness = PathStrokeThickness;

            foreach ( var style in Edge.Attr.Styles )
            {
                if ( style == Msagl.Uwp.UI.Layout.Style.Dotted )
                {
                    path.StrokeDashArray = new DoubleCollection { 1, 1 };
                }
                else if ( style == Msagl.Uwp.UI.Layout.Style.Dashed )
                {
                    var f = DashSize();
                    path.StrokeDashArray = new DoubleCollection { f, f };
                    //CurvePath.StrokeDashOffset = f;
                }
            }
        }

        Brush SetStrokeColorForRail( byte transparency, Rail rail )
        {
            return rail.IsHighlighted == false
                       ? new SolidColorBrush( new Windows.UI.Color
                       {
                           A = transparency,
                           R = Edge.Attr.Color.R,
                           G = Edge.Attr.Color.G,
                           B = Edge.Attr.Color.B
                       } )
                       : new SolidColorBrush( Colors.Red );
        }

        void SetPathStrokeToPath( Path path )
        {
            path.Stroke = new SolidColorBrush( Edge.Attr.Color );
            path.StrokeThickness = PathStrokeThickness;

            foreach ( var style in Edge.Attr.Styles )
            {
                if ( style == Msagl.Uwp.UI.Layout.Style.Dotted )
                {
                    path.StrokeDashArray = new DoubleCollection { 1, 1 };
                }
                else if ( style == Msagl.Uwp.UI.Layout.Style.Dashed )
                {
                    var f = DashSize();
                    path.StrokeDashArray = new DoubleCollection { f, f };
                    //CurvePath.StrokeDashOffset = f;
                }
            }
        }

        

        internal static double dashSize = 0.05; //inches
        internal Func<double> PathStrokeThicknessFunc;

        

        public FrameworkElement CreateFrameworkElementForRail( Rail rail, byte edgeTransparency )
        {
            var iCurve = rail.Geometry as ICurve;
            Path fe;

            if ( iCurve != null )
            {
                fe = (Path)CreateFrameworkElementForRailCurve( rail, iCurve, edgeTransparency );
            }
            else
            {
                var arrowhead = rail.Geometry as Arrowhead;

                if ( arrowhead != null )
                {
                    fe = (Path)CreateFrameworkElementForRailArrowhead( rail, arrowhead, rail.CurveAttachmentPoint, edgeTransparency );
                }
                else
                {
                    throw new InvalidOperationException();
                }
            }

            fe.Tag = rail;

            return fe;
        }

        public override string ToString()
        {
            return Edge.ToString();
        }

        internal double DashSize()
        {
            var w = PathStrokeThickness;
            var dashSizeInPoints = dashSize * GraphViewer.DpiXStatic;

            return dashSize = dashSizeInPoints / w;
        }

        internal void RemoveItselfFromCanvas( Canvas graphCanvas )
        {
            if ( CurvePath != null )
                graphCanvas.Children.Remove( CurvePath );

            if ( SourceArrowHeadPath != null )
                graphCanvas.Children.Remove( SourceArrowHeadPath );

            if ( TargetArrowHeadPath != null )
                graphCanvas.Children.Remove( TargetArrowHeadPath );

            if ( VLabel != null )
                graphCanvas.Children.Remove( VLabel.FrameworkElement );
        }

        

        FrameworkElement CreateFrameworkElementForRailArrowhead( Rail rail, Arrowhead arrowhead, Point curveAttachmentPoint, byte edgeTransparency )
        {
            var pathGeometry = new PathGeometry()
            {
                Figures = new PathFigureCollection()
            };

            AddArrow( pathGeometry, curveAttachmentPoint, arrowhead.TipPosition, PathStrokeThickness );

            var path = new Path
            {
                Data = pathGeometry,
                Tag = this
            };

            SetPathStrokeToRailPath( rail, path, edgeTransparency );

            return path;
        }

        FrameworkElement CreateFrameworkElementForRailCurve( Rail rail, ICurve iCurve, byte transparency )
        {
            var path = new Path
            {
                Data = GetICurveGeometry( iCurve ),
            };

            SetPathStrokeToRailPath( rail, path, transparency );

            return path;
        }
    }
}