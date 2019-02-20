//Imkc 2019.01.30
//出图界面将尺寸标注变为天正标注样式，对角线未多段线矩形形式
//使用范围：①适用于住宅等不倾斜角度项目，倾斜角度为测试
//②建议将标注样式单独创建，将记号与中心线记号样式改为无，
//在Array为2时，记号样式会体现，连续标注时。中心线符号会体现。理论未知，本人暂时猜测
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Autodesk.Revit.ApplicationServices;
using Autodesk.Revit.UI;
using Autodesk.Revit.DB;
using Autodesk.Revit.UI.Selection;
using Autodesk.Revit.Attributes;
using Autodesk.Revit.Creation;
using Document = Autodesk.Revit.DB.Document;

namespace GetDimsionEndpoint
{
    [Transaction(TransactionMode.Manual)]

    public class Command : IExternalCommand

    
    {
        Result IExternalCommand.Execute(ExternalCommandData commandData, ref string message, ElementSet elements)
        {
            UIDocument uidoc = commandData.Application.ActiveUIDocument;
            Document doc = uidoc.Document;
            Level level = doc.ActiveView.GenLevel;
            View view = doc.ActiveView;
            FamilySymbol symbol = doc.GetElement(new ElementId(587194)) as FamilySymbol;
            ElementClassFilter dimfilter = new ElementClassFilter(typeof(Dimension));
            ElementOwnerViewFilter viewfilter = new ElementOwnerViewFilter(view.Id);
            FilteredElementCollector collector = new FilteredElementCollector(doc);
            collector.WherePasses(dimfilter).WherePasses(viewfilter);
            List<Dimension> add = new List<Dimension>();
            List<Dimension> add2 = new List<Dimension>();
            List<DimensionSegmentArray> dimarrayarray = new List<DimensionSegmentArray>();
            ReferenceArray referarray = new ReferenceArray();
            ReferenceArray referarray2 = new ReferenceArray();
            foreach (Element e in collector)
            {
                Dimension dim = e as Dimension;
                if (dim.NumberOfSegments > 1)
                {
                    dimarrayarray.Add(dim.Segments);
                    add2.Add(dim);
                }
                else
                {
                    add.Add(dim);
                    
                }
            }
            if (add2.Count != 0)            //连续尺寸标注
            {

                using (Transaction trans = new Transaction(doc))
                {
                    FailureHandlingOptions failureHandlingOptions = trans.GetFailureHandlingOptions();

                    FailureHandler failureHandler = new FailureHandler();

                    failureHandlingOptions.SetFailuresPreprocessor(failureHandler);

                    // 这句话是关键  

                    failureHandlingOptions.SetClearAfterRollback(true);

                    trans.SetFailureHandlingOptions(failureHandlingOptions);


                    trans.Start("Start");
                    ChangeDimensionTypeStyle(add2.First<Dimension>(), doc);
                    for (int b = 0; b < add2.Count; b++)
                    {
                        referarray2 = add2.ElementAt<Dimension>(b).References;
                        foreach (Reference refer in referarray2)
                        {
                            //string re = refer.ElementReferenceType.ToString();
                            var cu = add2.ElementAt<Dimension>(b).Curve;
                            Dimension dim = add2.ElementAt<Dimension>(b);
                            Line li = cu as Line;
                            //var n = re.GetType().ToString();
                            Element el = doc.GetElement(refer);
                            Curve curve = null;
                            if (el.Location.GetType() != typeof(LocationCurve))
                            {
                                if (el.GetType() == typeof(Grid))
                                {
                                    curve = (el as Grid).Curve;
                                }
                                else if((el as FamilyInstance) is FamilyInstance)
                                {
                                    FamilyInstance instance2 = el as FamilyInstance;
                                    Element hostelement = instance2.Host;
                                    curve = GetLineFromLcPoint(GetSolid(hostelement), instance2);
                                }
                                else if (el.GetType() == typeof(Floor))
                                {
                                    Floor floor = el as Floor;
                                    Face face = GetGeometryFace(GetSolid(el));
                                    var test2 = GetCurveFromFloor(face, add2[b].Curve);
                                    if (test2.Size < 2)
                                    {
                                        break;
                                    }
                                    XYZ[] xYZsn = { test2.get_Item(0).GetEndPoint(0), test2.get_Item(1).GetEndPoint(0) };
                                    Line l3 = Line.CreateBound(test2.get_Item(0).GetEndPoint(0),
                                        test2.get_Item(1).GetEndPoint(0));
                                    curve = l3 as Curve;
                                }
                                
                            }
                            else
                            {
                                curve = (el.Location as LocationCurve).Curve;
                            }

                            if (curve == null)
                            {
                                break;
                            }
                            XYZ p0 = li.Project(curve.GetEndPoint(1))
                                .XYZPoint;
                            if (!symbol.IsActive)
                            {
                                symbol.Activate();
                            }

                            FamilyInstance instance = doc.Create.NewFamilyInstance(p0, symbol, level,
                                Autodesk.Revit.DB.Structure.StructuralType.UnknownFraming);
                            var l = GetGeometryLine(GetGeometryFace(GetSolid(instance)));
                            double an = GetAngle(li, l);
                            double rotateangle ;
                            double e = 1e-12;
                            double a = Math.Round((an - Math.PI / 4), 13);
                            double c = Math.Round(Math.Abs((an - Math.PI * 3 / 2)), 13);
                            if (a!=e ||c !=Math.Round(Math.PI*3/2,13)) //判断度数，相减做判断
                            {
                                if (an > Math.PI*0.5)
                                {
                                    rotateangle = 0.75* Math.PI - an;
                                }
                                else
                                {
                                    rotateangle = an + 0.25*Math.PI ;
                                }
                                LocationPoint lp = instance.Location as LocationPoint;
                                if (lp != null)
                                {
                                    Line line3 = Line.CreateBound(new XYZ(lp.Point.X, lp.Point.Y, 0), new XYZ(lp.Point.X, lp.Point.Y, lp.Point.Z + 20));
                                    lp.Rotate(line3,rotateangle);
                                    XYZ pNew = (instance.Location as LocationPoint).Point;
                                    if (p0.X != pNew.X || p0.Y != pNew.Y || p0.Z != pNew.Z)
                                    {
                                        lp.Point = p0;
                                    }
                                    //旋转基于平面与原点位置，对照两个位置将旋转后族放置到指定位置
                                }
                            }

                        }
                    }
                    trans.Commit();
                }
            }      //逐点标注
            if (add.Count != 0)           //普通尺寸标注
            {
                using (Transaction trans = new Transaction(doc))        //将本身的Dimension样式修改未无对角线样式
                {
                    FailureHandlingOptions failureHandlingOptions = trans.GetFailureHandlingOptions();

                    FailureHandler failureHandler = new FailureHandler();

                    failureHandlingOptions.SetFailuresPreprocessor(failureHandler);

                    // 这句话是关键  

                    failureHandlingOptions.SetClearAfterRollback(true);

                    trans.SetFailureHandlingOptions(failureHandlingOptions);


                    trans.Start("Text");
                    ChangeDimensionTypeStyle(add.First<Dimension>(), doc);
                    trans.Commit();
                }
                for (int a = 0; a < add.Count; a++)
                {

                    referarray = add.ElementAt<Dimension>(a).References;
                    if (referarray.Size == 2)
                    {
                        foreach (Reference re in referarray)
                        {
                            var cu = add.ElementAt<Dimension>(a).Curve;
                            Dimension dim = add.ElementAt<Dimension>(a);
                            Line li = cu as Line;
                            //var n = re.GetType().ToString();
                            Element el = doc.GetElement(re);
                            Curve curve = null;
                            using (Transaction test = new Transaction(doc, "change"))
                            {
                                FailureHandlingOptions failureHandlingOptions = test.GetFailureHandlingOptions();

                                FailureHandler failureHandler = new FailureHandler();

                                failureHandlingOptions.SetFailuresPreprocessor(failureHandler);

                                // 这句话是关键  

                                failureHandlingOptions.SetClearAfterRollback(true);

                                test.SetFailureHandlingOptions(failureHandlingOptions);
                                test.Start();
                                if (el.Location.GetType() != typeof(LocationCurve))
                                {
                                    if (el.GetType() == typeof(Grid))
                                    {
                                        curve = (el as Grid).Curve;
                                    }
                                    else if ((el as FamilyInstance) is FamilyInstance)
                                    {
                                        FamilyInstance instance2 = el as FamilyInstance;
                                        Element hostelement = instance2.Host;
                                        curve = GetLineFromLcPoint(GetSolid(hostelement), instance2);
                                    }
                                    else if (el.GetType() == typeof(Floor))
                                    {
                                        Floor floor = el as Floor;
                                        Face face = GetGeometryFace(GetSolid(el));
                                        var test2 = GetCurveFromFloor(face, add[a].Curve);
                                        if (test2.Size < 2)
                                        {
                                            break;
                                        }
                                        XYZ[] xYZsn = { test2.get_Item(0).GetEndPoint(0), test2.get_Item(1).GetEndPoint(0) };
                                        Line l = Line.CreateBound(test2.get_Item(0).GetEndPoint(0),
                                            test2.get_Item(1).GetEndPoint(0));
                                        curve = l as Curve;
                                    }

                                    if (curve == null)
                                    {
                                        break;
                                    }
                                    XYZ[] xYZs = { curve.GetEndPoint(0), curve.GetEndPoint(1) };
                                    foreach (XYZ xyz in xYZs)
                                    {
                                        XYZ p0 = li.Project(xyz).XYZPoint;
                                        if (!symbol.IsActive)
                                        {
                                            symbol.Activate();
                                        }

                                        FamilyInstance instance = doc.Create.NewFamilyInstance(p0, symbol, level,
                                            Autodesk.Revit.DB.Structure.StructuralType.UnknownFraming);
                                        var l = GetGeometryLine(GetGeometryFace(GetSolid(instance)));
                                        double an = GetAngle(li, l);
                                        if (an != Math.PI / 4 || an != Math.PI * 3 / 2)
                                        {
                                            LocationPoint lp = instance.Location as LocationPoint;
                                            if (lp != null)
                                            {
                                                Line line3 = Line.CreateBound(new XYZ(lp.Point.X, lp.Point.Y, 0), new XYZ(lp.Point.X, lp.Point.Y, lp.Point.Z + 20));
                                                lp.Rotate(line3, 0.25 * Math.PI + (Math.PI + an));
                                                XYZ pNew = (instance.Location as LocationPoint).Point;
                                                if (p0.X != pNew.X || p0.Y != pNew.Y || p0.Z != pNew.Z)
                                                {
                                                    lp.Point = p0;
                                                }
                                                //旋转基于平面与原点位置，对照两个位置将旋转后族放置到指定位置
                                            }
                                        }
                                    }
                                }
                                else
                                {
                                    curve = (el.Location as LocationCurve).Curve;
                                    if (doc.GetElement(referarray.get_Item(0)).Id ==
                                        doc.GetElement(referarray.get_Item(1)).Id)
                                    {
                                        var cu4 = add.ElementAt<Dimension>(a).Curve;
                                        Dimension dim4 = add.ElementAt<Dimension>(a);
                                        Line li4 = cu as Line;
                                        //var n = re.GetType().ToString();
                                        Element el4 = doc.GetElement(referarray.get_Item(0));
                                        Curve curve4 = (el4.Location as LocationCurve).Curve;
                                        XYZ[] xyzs2 = { curve4.GetEndPoint(0), curve4.GetEndPoint(1) };
                                        foreach (XYZ xyz in xyzs2)
                                        {
                                            XYZ p0 = li.Project(xyz).XYZPoint;
                                            if (!symbol.IsActive)
                                            {
                                                symbol.Activate();
                                            }

                                            FamilyInstance instance = doc.Create.NewFamilyInstance(p0, symbol, level,
                                                Autodesk.Revit.DB.Structure.StructuralType.UnknownFraming);
                                            var l = GetGeometryLine(GetGeometryFace(GetSolid(instance)));
                                            double an = GetAngle(li, l);
                                            if (an != Math.PI / 4 || an != Math.PI * 3 / 2)
                                            {
                                                LocationPoint lp = instance.Location as LocationPoint;
                                                if (lp != null)
                                                {
                                                    Line line3 = Line.CreateBound(new XYZ(lp.Point.X, lp.Point.Y, 0), new XYZ(lp.Point.X, lp.Point.Y, lp.Point.Z + 20));
                                                    lp.Rotate(line3, 0.25 * Math.PI + (Math.PI + an));
                                                    XYZ pNew = (instance.Location as LocationPoint).Point;
                                                    if (p0.X != pNew.X || p0.Y != pNew.Y || p0.Z != pNew.Z)
                                                    {
                                                        lp.Point = p0;
                                                    }
                                                    //旋转基于平面与原点位置，对照两个位置将旋转后族放置到指定位置
                                                }
                                            }
                                        }
                                    }
                                    else
                                    {
                                        foreach (Reference refer in referarray)
                                        {
                                            //string re = refer.ElementReferenceType.ToString();
                                            var cu3 = add.ElementAt<Dimension>(a).Curve;
                                            Dimension dim3 = add.ElementAt<Dimension>(a);
                                            Line li3 = cu as Line;
                                            //var n = re.GetType().ToString();
                                            Element el3 = doc.GetElement(refer);
                                            Curve curve3 = null;
                                            if (el3.Location.GetType() != typeof(LocationCurve))
                                            {
                                                if (el3.GetType() == typeof(Grid))
                                                {
                                                    curve3 = (el3 as Grid).Curve;
                                                }
                                                else if ((el3 as FamilyInstance) is FamilyInstance)
                                                {
                                                    FamilyInstance instance3 = el3 as FamilyInstance;
                                                    Element hostelement = instance3.Host;
                                                    curve3 = GetLineFromLcPoint(GetSolid(hostelement), instance3);
                                                }
                                                else if (el3.GetType() == typeof(Floor))
                                                {
                                                    Floor floor = el3 as Floor;
                                                    Face face = GetGeometryFace(GetSolid(el3));
                                                    var test2 = GetCurveFromFloor(face, add2[a].Curve);
                                                    if (test2.Size < 2)
                                                    {
                                                        break;
                                                    }
                                                    XYZ[] xYZsn = { test2.get_Item(0).GetEndPoint(0), test2.get_Item(1).GetEndPoint(0) };
                                                    Line l4 = Line.CreateBound(test2.get_Item(0).GetEndPoint(0),
                                                        test2.get_Item(1).GetEndPoint(0));
                                                    curve3 = l4 as Curve;
                                                }
                                            }
                                            else
                                            {

                                                curve3 = (el3.Location as LocationCurve).Curve;


                                            }

                                            if (curve3 == null)
                                            {
                                                break;
                                            }
                                            XYZ p0 = li3.Project(curve.GetEndPoint(1))
                                                .XYZPoint;
                                            if (!symbol.IsActive)
                                            {
                                                symbol.Activate();
                                            }

                                            FamilyInstance instance = doc.Create.NewFamilyInstance(p0, symbol, level,
                                                Autodesk.Revit.DB.Structure.StructuralType.UnknownFraming);
                                            var l = GetGeometryLine(GetGeometryFace(GetSolid(instance)));
                                            double an = GetAngle(li, l);
                                            if (an != Math.PI / 4 || an != Math.PI * 3 / 2)
                                            {
                                                LocationPoint lp = instance.Location as LocationPoint;
                                                if (lp != null)
                                                {
                                                    Line line3 = Line.CreateBound(new XYZ(lp.Point.X, lp.Point.Y, 0), new XYZ(lp.Point.X, lp.Point.Y, lp.Point.Z + 20));
                                                    lp.Rotate(line3, 0.25 * Math.PI + (Math.PI + an));
                                                    XYZ pNew = (instance.Location as LocationPoint).Point;
                                                    if (p0.X != pNew.X || p0.Y != pNew.Y || p0.Z != pNew.Z)
                                                    {
                                                        lp.Point = p0;
                                                    }
                                                    //旋转基于平面与原点位置，对照两个位置将旋转后族放置到指定位置
                                                }
                                            }

                                        }
                                    }

                                }

                                test.Commit();
                            }

                        }

                    }
                    
                }
            }         //连续标注
            return Result.Succeeded;
        }
        /// <summary>
        /// 获取形体
        /// </summary>
        /// <param name="instance"></param>
        /// <returns></returns>
        private Solid GetSolid(FamilyInstance instance)
        {
            Solid solid = null;
            Options option = new Options();
            option.ComputeReferences = true;
            option.DetailLevel = ViewDetailLevel.Fine;
            GeometryElement geo = null;
            geo = instance.Symbol.get_Geometry(option);      //需要获取Symbol才能获取Geometry。或许是因为常规模型的原因。待解决
            foreach (GeometryObject g in geo)
            {
                if (g is Solid)
                {
                    solid = g as Solid;
                    break;

                }
                else if (g is GeometryInstance)
                {
                    GeometryInstance geometryInstance = g as GeometryInstance;
                    GeometryElement geometryElement = geometryInstance.GetInstanceGeometry();
                    foreach (GeometryObject gt in geometryElement)
                    {
                        solid = gt as Solid;
                        break;
                    }
                }
            }

            return solid;
        }

        private Solid GetSolid(Element element)
        {
            Solid solid = null;
            Options option = new Options();
            option.ComputeReferences = true;
            option.DetailLevel = ViewDetailLevel.Fine;
            GeometryElement geo = null;
            geo = element.get_Geometry(option);      //需要获取Symbol才能获取Geometry。或许是因为常规模型的原因。待解决
            foreach (GeometryObject g in geo)
            {
                if (g is Solid)
                {
                    solid = g as Solid;
                    break;

                }
                else if (g is GeometryInstance)
                {
                    GeometryInstance geometryInstance = g as GeometryInstance;
                    GeometryElement geometryElement = geometryInstance.GetInstanceGeometry();
                    foreach (GeometryObject gt in geometryElement)
                    {
                        solid = gt as Solid;
                        break;
                    }
                }
            }

            return solid;
        }
        /// <summary>
        /// 获取面
        /// </summary>
        /// <param name="solid"></param>
        /// <returns></returns>
        private Face GetGeometryFace(Solid solid)
        {
            Line line = null;
            PlanarFace pf = null;
            if (solid != null)
            {
                
                foreach (Face fa in solid.Faces)
                {
                    pf = fa as PlanarFace;
                    if (pf != null)
                    {
                        if (Math.Abs(pf.FaceNormal.X) < 0.01 && Math.Abs(pf.FaceNormal.Y) < 0.01 && pf.FaceNormal.Z > 0) //Z=-1为最低Z=1为最高    Z轴
                        {
                            
                            break;
                        }
                    }
                }


            }

            return pf;
        }
        /// <summary>
        /// 获取线
        /// </summary>
        /// <param name="face"></param>
        /// <returns></returns>
        private Line GetGeometryLine(Face face)
        {
            EdgeArrayArray edgeArrayArray = face.EdgeLoops;
            List<Line> linelist = new List<Line>();
            Dictionary<Line, double> dic = new Dictionary<Line, double>();
            foreach (EdgeArray array in edgeArrayArray)
            {
                foreach (Edge e in array)
                {
                    Line li = e.AsCurve() as Line;
                    if (li == null)
                    {
                        break;
                    }
                    dic.Add(li, li.Length);
                }
            }

            Line line = null;
            var liLength = dic.Values.Max();
            for (int a = 0; a < dic.Count; a++)
            {
                if (dic.ElementAt(a).Value == liLength)
                {
                    line = dic.ElementAt(a).Key;
                    break;
                }

                break;
            }

            return line;
        }
        /// <summary>
        /// 获取角度
        /// </summary>
        /// <param name="line1"></param>
        /// <param name="line2"></param>
        public double GetAngle(Line line1, Line line2)  //LIne为参考线Reference与矩形块外边界线
        {
            double angle = line1.Direction.AngleTo(line2.Direction);

            return angle;
        }
        /// <summary>
        /// 通过修改标注样式，将对角线取消
        /// </summary>
        /// <param name="dimension"></param>
        /// <param name="document"></param>
        private void ChangeDimensionTypeStyle(Dimension dimension, Document document)
        {
            var objType = dimension.DimensionType;
            Parameter para = objType.get_Parameter(BuiltInParameter.DIM_STYLE_CENTERLINE_TICK_MARK);  //中心线记号
            Parameter para2 = objType.get_Parameter(BuiltInParameter.DIM_LEADER_ARROWHEAD);//记号
            var a = para2.AsElementId();
            var c = para.Set(new ElementId(-1));
            var b = a.ToString();
        }
        /// <summary>
        /// 获取Location定位的端点，进行后续投影
        /// </summary>
        /// <param name="solid"></param>
        /// <param name="instance"></param>
        private Curve GetLineFromLcPoint(Solid solid,FamilyInstance instance)
        {
            PlanarFace pf = null;
            Dictionary<PlanarFace, double> dic_face = new Dictionary<PlanarFace, double>();
            foreach (Face fa in solid.Faces)
            {
                pf = fa as PlanarFace;
                dic_face.Add(pf, fa.Area);         //将面与面积值进行排序，筛选目标曲线
            }

            PlanarFace pf2 = null;
            if (dic_face.Count > 6)       //确定有门窗附着，筛选Host
            {
                FamilySymbol symbol = instance.Symbol;
                Parameter para_heigh = symbol.get_Parameter(BuiltInParameter.GENERIC_HEIGHT);
                double heigh = para_heigh.AsDouble();
                Parameter para_width = symbol.get_Parameter(BuiltInParameter.CASEWORK_WIDTH);
                double width = para_width.AsDouble();
                Dictionary<PlanarFace, double> dic_Zvalues = new Dictionary<PlanarFace, double>();
                if (heigh > width)
                {
                    for (int a = 0; a < dic_face.Count; a++)
                    {
                        pf2 = dic_face.Keys.ElementAt<PlanarFace>(a);
                        if (dic_face.Values.ElementAt<double>(a) == dic_face.Values.Min())
                        {
                            break;
                        }
                    }
                }
                else if (heigh< width)
                {
                    for (int b = 0; b < dic_face.Count; b++)
                    {
                        if (dic_face.Values.ElementAt<double>(b) == dic_face.Values.Min())
                        {
                            PlanarFace p = dic_face.Keys.ElementAt<PlanarFace>(b);
                            dic_face.Remove(p);
                        }
                    }

                    for (int c = 0; c < dic_face.Count; c++)
                    {
                        pf2 = dic_face.Keys.ElementAt<PlanarFace>(c);
                        if (dic_face.Values.ElementAt<double>(c) == dic_face.Values.Min())
                        {
                            break;
                        }
                    }
                }
                else if (heigh == width)
                {
                    for (int d = 0; d < dic_face.Count; d++)
                    {
                        if (dic_face.Values.ElementAt<double>(d) == dic_face.Values.Min())
                        {
                            dic_Zvalues.Add(dic_face.Keys.ElementAt<PlanarFace>(d),
                                dic_face.Keys.ElementAt<PlanarFace>(d).Origin.Z);
                        }
                    }

                    for (int e = 0; e < dic_Zvalues.Count; e++)
                    {
                        pf2 = dic_Zvalues.Keys.ElementAt<PlanarFace>(e);
                        if (dic_Zvalues.Values.ElementAt<double>(e) == dic_Zvalues.Values.Min())
                        {
                            break;
                        }
                    }
                }
            }
            //通过dictionary筛选出底部最长边，选取出两个点作为投影点
            Dictionary<Curve, double> dic_line = new Dictionary<Curve, double>();
            Curve curve = null;
            if (pf2 != null)
            {
                EdgeArrayArray arrayArray = pf2.EdgeLoops;
                foreach (EdgeArray edarray in arrayArray)
                {
                    foreach (Edge e in edarray)
                    {
                        Line line = (e.AsCurve()) as Line;
                        dic_line.Add(line, line.Length);
                    }
                }

                if (dic_line.Count != 0)
                {
                    for (int h = 0; h < dic_line.Count; h++)
                    {
                        curve = dic_line.Keys.ElementAt<Curve>(h);
                        if (dic_line.Values.ElementAt<double>(h) == dic_line.Values.Max())
                        {
                            break;
                        }
                    }
                }
            }

            return curve;
        }
        /// <summary>
        /// 筛选出楼板引出标注的位置，进行定位
        /// </summary>
        /// <param name="face"></param>
        /// <param name="curve"></param>
        /// <returns></returns>
        private CurveArray GetCurveFromFloor(Face face, Curve curve)
        {
            CurveArray curvearray = new CurveArray();
            Line l2 = curve as Line;
            EdgeArrayArray arrayArray = face.EdgeLoops;
            Dictionary<Curve, double> dic_angle = new Dictionary<Curve, double>();
            foreach (EdgeArray array in arrayArray)
            {
                foreach (Edge e in array)
                {
                    Curve cu = e.AsCurve();
                    Line l1 = cu as Line;
                    if (cu.GetType() != typeof(Line))
                    {
                        break;
                    }
                    var angle = GetAngle(l1, l2);
                    dic_angle.Add(cu, angle);                //将角度与对应曲线加入字典，之后根据相应数值进行提取
                }
            }

            double angle_a = Math.PI / 2;

            for (int a = 0; a < dic_angle.Count; a++)
            {
                if (dic_angle.Values.ElementAt<double>(a) == angle_a)
                {
                    curvearray.Append(dic_angle.Keys.ElementAt<Curve>(a));
                }
            }


            return curvearray;

        }

        public class FailureHandler : IFailuresPreprocessor

        {

            public string ErrorMessage { set; get; }

            public string ErrorSeverity { set; get; }



            public FailureHandler()

            {

                ErrorMessage = "";

                ErrorSeverity = "";

            }



            public FailureProcessingResult PreprocessFailures(FailuresAccessor failuresAccessor)

            {

                IList<FailureMessageAccessor> failureMessages = failuresAccessor.GetFailureMessages();



                foreach (FailureMessageAccessor failureMessageAccessor in failureMessages)

                {

                    // We're just deleting all of the warning level   

                    // failures and rolling back any others  



                    FailureDefinitionId id = failureMessageAccessor.GetFailureDefinitionId();



                    try

                    {

                        ErrorMessage = failureMessageAccessor.GetDescriptionText();

                    }

                    catch

                    {

                        ErrorMessage = "Unknown Error";

                    }



                    try

                    {

                        FailureSeverity failureSeverity = failureMessageAccessor.GetSeverity();



                        ErrorSeverity = failureSeverity.ToString();



                        if (failureSeverity == FailureSeverity.Warning)

                        {

                            // 如果是警告，则禁止消息框  

                            failuresAccessor.DeleteWarning(failureMessageAccessor);

                        }

                        else

                        {

                            // 如果是错误：则取消导致错误的操作，但是仍然继续整个事务  

                            return FailureProcessingResult.ProceedWithRollBack;

                        }

                    }

                    catch

                    {

                    }

                }

                return FailureProcessingResult.Continue;

            }

        }




    }
}
