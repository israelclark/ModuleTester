using Autodesk.AutoCAD.ApplicationServices;
using Autodesk.AutoCAD.DatabaseServices;
using Autodesk.AutoCAD.EditorInput;
using Autodesk.AutoCAD.Runtime;
using Autodesk.AutoCAD.Geometry;
using Autodesk.AutoCAD.Colors;
using acadApp = Autodesk.AutoCAD.ApplicationServices.Application;
using Autodesk.AutoCAD;
using Autodesk.AutoCAD.Internal;
//using Autodesk.AutoCAD.Interop;
//using Autodesk.AutoCAD.Interop.Common;    
using Autodesk.AutoCAD.Windows;
//using System.Linq;
using System.Text;
using System.Windows.Forms;
using System.Drawing;
using System.Collections.Generic;

//using System.Collections.Specialized;
using System.Runtime;
using System.Runtime.InteropServices;
using System;

//unique for Matrix Functions
using System.Reflection;

//unique for UCSSpy
using System.Collections;
//using System.Reflection;

//unique for CombineBlocksIntoLibrary
using System.IO;
using HSKDICommon;

//unique for DisableRibbon AcWindows, AdWindows, PresenationCore, PresentationFramework, WindowsBase
using Autodesk.Windows;


namespace HSKDIProject
{
    public enum IrrigationObjectType
    {
        Valve,
        SprayHead,
        Lateral,
        Mainline,
        Pump
    };

    public class ModuleCommands
    {
        [CommandMethod("InterpolatePoint")]
        public static void InterpolatePoint()
        {
            Document doc = acadApp.DocumentManager.MdiActiveDocument;
            Database db = doc.Database;
            Editor ed = doc.Editor;

            PromptEntityOptions peoXref = new PromptEntityOptions("\nSelect Grading Xref.");
            peoXref.AllowObjectOnLockedLayer = true;
            peoXref.AllowNone = false;            

            PromptEntityOptions peoPolyLine1 = new PromptEntityOptions("\nSelect polyline 1.");
            PromptEntityOptions peoPolyLine2 = new PromptEntityOptions("\nSelect polyline 2.");
            PromptPointOptions ppo = new PromptPointOptions("\nSelect Point of intrest.");

            PromptPointResult ppr = ed.GetPoint(ppo);
            if (ppr.Status == PromptStatus.OK)
            {
                Transaction tr = doc.TransactionManager.StartTransaction();
                using (tr)
                {
                    try
                    {
                        Point3d pt = ppr.Value;
                        BlockTable bt = (BlockTable)tr.GetObject(db.BlockTableId, OpenMode.ForRead);
                        PromptEntityResult perXref = ed.GetEntity(peoXref);
                        if (perXref.Status == PromptStatus.OK)
                        {
                            BlockReference xrefRef = (BlockReference)tr.GetObject(perXref.ObjectId, OpenMode.ForRead);
                            if (xrefRef != null)
                            {
                                // If so, we check whether the block table record to which it refers is actually from an XRef                         

                                ObjectId xrefId = xrefRef.BlockTableRecord;
                                BlockTableRecord xrefBTR = (BlockTableRecord)tr.GetObject(xrefId, OpenMode.ForRead);
                                if (xrefBTR != null)
                                {
                                    if (xrefBTR.IsFromExternalReference)
                                    {
                                        // If so, then we prigrammatically select the object underneath the pick-point already used                                    
                                        PromptNestedEntityOptions pneo = new PromptNestedEntityOptions("");
                                        pneo.NonInteractivePickPoint = perXref.PickedPoint;
                                        pneo.UseNonInteractivePickPoint = true;

                                        PromptNestedEntityResult pner = ed.GetNestedEntity(pneo);
                                        if (pner.Status == PromptStatus.OK)
                                        {
                                            try
                                            {
                                                ObjectId selId = pner.ObjectId;

                                                // Let's look at this programmatically-selected object, to see what it is
                                                DBObject obj = tr.GetObject(selId, OpenMode.ForRead);

                                                // If it's a polyline vertex, we need to go one level up to the polyline itself

                                                if (obj is PolylineVertex3d || obj is Vertex2d)
                                                    selId = obj.OwnerId;

                                                // We don't want to do anything at all for textual stuff, let's also make sure we are 
                                                // dealing with an entity (should always be the case)

                                                if (obj is MText || obj is DBText || !(obj is Entity))
                                                    return;

                                                // Now let's get the name of the layer, to use later

                                                Entity ent = (Entity)obj;
                                                LayerTableRecord ltr = (LayerTableRecord)tr.GetObject(ent.LayerId, OpenMode.ForRead);
                                                string layName = ltr.Name;

                                                ed.WriteMessage("\nObject Selected is {0} on layer {1} in xref {2}.", selId.GetType(), layName, xrefBTR.Name);
                                            }
                                            catch
                                            {
                                                // A number of innocuous things could go wrong
                                                // so let's not worry about the details

                                                // In the worst case we are simply not trying
                                                // to replace the entity, so OFFSET will just
                                                // reject the selected Xref
                                            }
                                        }
                                    }
                                }
                            }
                        }
                    }
                    catch
                    {
                        ed.WriteMessage("\nFailed in xrefSelect");
                    }
                    //BlockTableRecord ms = (BlockTableRecord)tr.GetObject(bt[BlockTableRecord.ModelSpace], OpenMode.ForWrite);
                }
                tr.Commit();
            }
            else
            {
                ed.WriteMessage("Failed to find point of intrest.");
            }
        }

        public static Point3d InterpolatePoint(Polyline A, Polyline B, Point3d p)
        {
            Document doc = acadApp.DocumentManager.MdiActiveDocument;
            Editor ed = doc.Editor;
            
            Point3d a = HSKDICommon.Commands.ClosestPtOnSegment(p, A);
            Point3d b = HSKDICommon.Commands.ClosestPtOnSegment(p, B);
            
            double XYdist_a_p = Math.Abs(a.X - p.X) + Math.Abs(a.Y - p.Y);
            double XYdist_a_b = Math.Abs(a.X - b.X) + Math.Abs(a.Y - b.Y);
            double xyDistPercentageFromA = XYdist_a_p/XYdist_a_b;            

            double c_Z = a.Z + xyDistPercentageFromA * Math.Abs(a.Z - b.Z);

            Point3d c = new Point3d(p.X, p.Y, c_Z);

            ed.WriteMessage("\nPoint ({0},{1},{2}) has been corrected to ({3},{4},{5}).", p.X, p.Y, p.Z, c.X, c.Y, c.Z);

            return c;
        }



        #region FieldLinking
        //[CommandMethod("GetFieldLink")]
        //static public void GetFieldLink()
        //{
        //    Document doc = acadApp.DocumentManager.MdiActiveDocument;
        //    Database db = doc.Database;
        //    Editor ed = doc.Editor;

        //    // Ask the user to select an attribute or an mtext
        //    PromptEntityOptions opt = new PromptEntityOptions("\nSelect an MText object containing field(s): ");
        //    opt.SetRejectMessage("\nObject must be MText.");

        //    opt.AddAllowedClass(typeof(MText), false);
        //    opt.AddAllowedClass(typeof(MLeader), false);

        //    PromptEntityResult res = ed.GetEntity(opt);
        //    if (res.Status == PromptStatus.OK)
        //    {
        //        Transaction tr = doc.TransactionManager.StartTransaction();
        //        using (tr)
        //        {
        //            // Check the entity is an MText object
        //            DBObject obj = tr.GetObject(res.ObjectId, OpenMode.ForRead);
        //            MText mt = obj as MText;

        //            if (mt != null)
        //            {
        //                if (!mt.HasFields)
        //                {
        //                    ed.WriteMessage("\nMText object does not contain fields.");
        //                }
        //                else
        //                {
        //                    // Open the extension dictionary
        //                    DBDictionary extDict = (DBDictionary)tr.GetObject(mt.ExtensionDictionary, OpenMode.ForRead);
        //                    const string fldDictName = "ACAD_FIELD";
        //                    const string fldEntryName = "TEXT";

        //                    // Get the field dictionary
        //                    if (extDict.Contains(fldDictName))
        //                    {
        //                        ObjectId fldDictId = extDict.GetAt(fldDictName);
        //                        if (fldDictId != ObjectId.Null)
        //                        {
        //                            DBDictionary fldDict = (DBDictionary)tr.GetObject(fldDictId, OpenMode.ForRead);
        //                            // Get the field itself
        //                            if (fldDict.Contains(fldEntryName))
        //                            {
        //                                ObjectId fldId = fldDict.GetAt(fldEntryName);
        //                                if (fldId != ObjectId.Null)
        //                                {
        //                                    obj = tr.GetObject(fldId, OpenMode.ForRead);
        //                                    Field fld = obj as Field;
        //                                    if (fld != null)
        //                                    {
        //                                        // And finally get the string
        //                                        // including the field codes
        //                                        string fldCode = fld.GetFieldCode();
        //                                        ed.WriteMessage("\nField code: " + fldCode);
        //                                        // Loop, using our helper function
        //                                        // to find the object references
        //                                        do
        //                                        {
        //                                            ObjectId objId;
        //                                            fldCode = FindObjectId(fldCode, out objId);
        //                                            if (fldCode != "")
        //                                            {
        //                                                // Print the ObjectId
        //                                                ed.WriteMessage("\nFound Object ID: " + objId.ToString());
        //                                                obj = tr.GetObject(objId, OpenMode.ForRead);
        //                                                // ... and the type of the object

        //                                                ed.WriteMessage(", which is an object of type " + obj.GetType().ToString());
        //                                            }
        //                                        } while (fldCode != "");
        //                                    }
        //                                }
        //                            }
        //                        }
        //                    }
        //                }
        //            }
        //        }
        //    }
        //}
        //static public string FindObjectId(string text, out ObjectId objId)
        //{
        //    const string prefix = "%<\\_ObjId ";
        //    const string suffix = ">%";

        //    // Find the location of the prefix string
        //    int preLoc = text.IndexOf(prefix);
        //    if (preLoc > 0)
        //    {
        //        // Find the location of the ID itself
        //        int idLoc = preLoc + prefix.Length;

        //        // Get the remaining string
        //        string remains = text.Substring(idLoc);

        //        // Find the location of the suffix
        //        int sufLoc = remains.IndexOf(suffix);

        //        // Extract the ID string and get the ObjectId
        //        string id = remains.Remove(sufLoc);
        //        objId = new ObjectId((IntPtr)Convert.ToInt64(id));

        //        // Return the remainder, to allow extraction
        //        // of any remaining IDs
        //        return remains.Substring(sufLoc + suffix.Length);
        //    }

        //    else
        //    {
        //        objId = ObjectId.Null;
        //        return "";
        //    }
        //}

        #endregion

        [CommandMethod("FlattenBlock")]
        public void FlattenBlock()
        {
            // Flattens nested blocks into single block with all appropriate attributes preserved
        }

        #region DisableRibbon
        private static bool _showTipsOnDisabled = false;

        [CommandMethod("DR")]
        public static void DisableRibbonCommand()
        {
            EnableRibbon(false);
        }

        [CommandMethod("ER")]
        public static void EnableRibbonCommand()
        {
            EnableRibbon(true);
        }

        public static void EnableRibbon(bool enable)
        {
            // Start by making sure we have a ribbon
            // (if calling from a command this will almost certainly just return
            // the ribbin that already exists)

            var rps = Autodesk.AutoCAD.Ribbon.RibbonServices.CreateRibbonPaletteSet();

            // Enable or disable it

            rps.RibbonControl.IsEnabled = enable;

            if (!enable)
            {
                // Store the current setting for "Show tooltips when the ribbon is disabled"
                // and then modify the setting

                _showTipsOnDisabled = ComponentManager.ToolTipSettings.ShowOnDisabled;
                ComponentManager.ToolTipSettings.ShowOnDisabled = enable;
            }
            else
            {
                // Restore the setting for "Show tooltips when the ribbon is disabled"

                ComponentManager.ToolTipSettings.ShowOnDisabled = _showTipsOnDisabled;
            }

            // Enable or disable background tab rendering

            rps.RibbonControl.IsBackgroundTabRenderingEnabled = enable;
        }
        #endregion
                
        #region ReadText&Mtxt
        //[Autodesk.AutoCAD.Runtime.CommandMethod("ReadTxt", CommandFlags.Session)]
        //public string ReadTxt()
        //{
        //    Document doc = acadApp.DocumentManager.MdiActiveDocument;
        //    Database db = doc.Database;
        //    Editor ed = doc.Editor;

        //    PromptEntityOptions peo = new PromptEntityOptions("\nSelect Text or Mtext object: ");

        //    string myText = null;

        //    Transaction tr = db.TransactionManager.StartTransaction();
        //    using (tr)
        //    {
        //        try
        //        {
        //            PromptEntityResult per = ed.GetEntity(peo);
        //            if (per.ObjectId != null)
        //            {
        //                Entity ent = (Entity)tr.GetObject(per.ObjectId, OpenMode.ForRead, false, true);
        //                ed.WriteMessage("\n Entity Type: {0}", ent.GetType().ToString());

        //                switch (ent.GetType().ToString())
        //                {
        //                    case "Autodesk.AutoCAD.DatabaseServices.DBText":
        //                        DBText textObj = (DBText)ent;
        //                        myText = textObj.TextString;
        //                        ed.WriteMessage("\nInteral Text:\n" + myText);
        //                        textObj.Dispose();
        //                        break;
        //                    case "Autodesk.AutoCAD.DatabaseServices.MText":
        //                        MText mtextObj = (MText)ent;
        //                        myText = mtextObj.Text;
        //                        ed.WriteMessage("\nInteral Text:\n" + myText);
        //                        mtextObj.Dispose();
        //                        break;
        //                    case "Autodesk.AutoCAD.DatabaseServices.MLeader":
        //                        MLeader mLeaderObj = (MLeader)ent;
        //                        if (mLeaderObj.MText.Contents != null)
        //                        {
        //                            myText = mLeaderObj.MText.Text;
        //                            ed.WriteMessage("\nInteral Text:\n" + myText);
        //                        }
        //                        else if (mLeaderObj.BlockName != "")
        //                        {
        //                            ObjectId blkid = mLeaderObj.BlockId;

        //                        }
        //                        else ed.WriteMessage("\nThis multileader does not have text objects inside.");
        //                        mLeaderObj.Dispose();
        //                        break;
        //                    default:
        //                        ed.WriteMessage("\nThis is not a text object.");
        //                        break;
        //                }
        //            }
        //        }
        //        catch
        //        {
        //        }
        //        finally
        //        {
        //            tr.Commit();
        //        }
        //        return myText;
        //    }
        //}
        #endregion

        #region HeadRow


        //public Polyline FlipPolyLine(Polyline pl)
        //{
        //    //create a polyline that has all the properties of the input
        //    //but only has dummy start & end points - to be removed later.
        //    Polyline flippedPL = (Polyline)pl.Clone();
        //    for (int i = 1; i < flippedPL.NumberOfVertices - 1; i++)
        //    {
        //        flippedPL.RemoveVertexAt(i);
        //    }

        //    Document doc = acadApp.DocumentManager.MdiActiveDocument;
        //    Database db = doc.Database;
        //    Editor ed = doc.Editor;

        //    PolylineProps plP = PolylinePropsCollection(pl);

        //    plP.pts.Reverse();
        //    plP.blgs.Reverse();
        //    List<double> temp = plP.swdths;
        //    plP.swdths = plP.ewdths;
        //    plP.ewdths = temp;
        //    plP.swdths.Reverse();
        //    plP.ewdths.Reverse();

        //    for (int i = 0; i <= pl.NumberOfVertices - 1; i++)
        //    {
        //        flippedPL.AddVertexAt(i, new Point2d(plP.pts[i].X, plP.pts[i].Y), plP.blgs[i], plP.swdths[i], plP.ewdths[i]);
        //    }

        //    flippedPL.RemoveVertexAt(1);
        //    flippedPL.RemoveVertexAt(0);

        //    return flippedPL;
        //}

        //public List<Polyline> UnwrapPolyLine(Polyline pl, Point3d pt1, Point3d pt2)
        //{
        //    //create a pair of polylines that has all the properties of the input
        //    //but only have dummy start & end points - to be removed later.


        //    Polyline newPL1 = (Polyline)pl.Clone();

        //    for (int i = 1; i < newPL1.NumberOfVertices - 1; i++)
        //    {
        //        newPL1.RemoveVertexAt(i);
        //    }
        //    Polyline newPL2 = (Polyline)newPL1.Clone();

        //    ////deconstruct polyline into points, buldges, widths
        //    //Point3dCollection pts = new Point3dCollection();
        //    //Vector3dCollection vects = new Vector3dCollection();
        //    //DoubleCollection widths = new DoubleCollection();
        //    //Point3d currentVert = new Point3d();
        //    Document doc = acadApp.DocumentManager.MdiActiveDocument;
        //    Database db = doc.Database;
        //    Editor ed = doc.Editor;

        //    PolylineProps plP = PolylinePropsCollection(pl);

        //    plP.pts.Reverse();
        //    plP.blgs.Reverse();
        //    List<double> temp = plP.swdths;
        //    plP.swdths = plP.ewdths;
        //    plP.ewdths = temp;
        //    plP.swdths.Reverse();
        //    plP.ewdths.Reverse();

        //    for (int i = 0; i <= pl.NumberOfVertices - 1; i++)
        //    {
        //        flippedPL.AddVertexAt(i, new Point2d(plP.pts[i].X, plP.pts[i].Y), plP.blgs[i], plP.swdths[i], plP.ewdths[i]);
        //    }

        //    flippedPL.RemoveVertexAt(1);
        //    flippedPL.RemoveVertexAt(0);

        //    return flippedPL;
        //}

        //private static PolylineProps PolylinePropsCollection(Polyline pl)
        //{
        //    //pts = new Point3dCollection();
        //    //blgs = new DoubleCollection();
        //    //swdths = new DoubleCollection();
        //    //ewdths = new DoubleCollection();
        //    PolylineProps plP = new PolylineProps();

        //    for (int i = 0; i <= pl.NumberOfVertices - 1; i++)
        //    {
        //        plP.pts.Add(pl.GetPoint3dAt(i));
        //        plP.blgs.Add(pl.GetBulgeAt(i));
        //        plP.swdths.Add(pl.GetStartWidthAt(i));
        //        plP.ewdths.Add(pl.GetEndWidthAt(i));
        //    }

        //    return plP;
        //}

        //public Polyline TruncatePolyLine(Polyline pl, Point3d newStartPt, Point3d newEndPt, bool extend)
        //{
        //    Polyline truncPL = (Polyline)pl.Clone();
        //    Point3d nearStartPt = pl.GetClosestPointTo(newStartPt, extend);
        //    Point3d nearEndPt = pl.GetClosestPointTo(newEndPt, extend);

        //    int startIndex = 0, endIndex = 0;

        //    int plStartSegment = -1, plEndSegment = -1;
        //    for (int i = 0; i <= pl.NumberOfVertices - 2; i++)
        //    {
        //        if (pl.GetBulgeAt(i) == 0)
        //        {
        //            LineSegment3d lineS = pl.GetLineSegmentAt(i);

        //            if (lineS.IsColinearTo(new Line3d(pl.GetPoint3dAt(i), newStartPt))) plStartSegment = i;
        //            if (lineS.IsColinearTo(new Line3d(pl.GetPoint3dAt(i), newEndPt))) plEndSegment = i;

        //        }
        //        else
        //        {
        //            if (pl.GetArcSegmentAt(i).IsOn(newStartPt)) plStartSegment = i;
        //            if (pl.GetArcSegmentAt(i).IsOn(newEndPt)) plEndSegment = i;
        //        }

        //    }

        //    if (plStartSegment > plEndSegment)
        //    {
        //        truncPL = FlipPolyLine(truncPL);
        //        //pl.UpgradeOpen();
        //        //pl = FlippedPolyLine(pl);
        //        //pl.DowngradeOpen();
        //    }

        //    for (int i = plStartSegment; i <= plEndSegment; i++)
        //    {
        //        if (i > 0)
        //        {
        //            if (new Line3d(pl.GetPoint3dAt(i - 1), pl.GetPoint3dAt(i)).IsColinearTo(new Line3d(pl.GetPoint3dAt(i - 1), nearStartPt)))
        //                //pl.UpgradeOpen();
        //                truncPL.AddVertexAt(i,
        //                                new Point2d(nearStartPt.X, nearStartPt.Y),
        //                                pl.GetBulgeAt(i - 1) * pl.GetPoint3dAt(i - 1).DistanceTo(nearStartPt) / pl.GetPoint3dAt(i).DistanceTo(nearStartPt),
        //                                pl.GetStartWidthAt(i - 1) + pl.GetEndWidthAt(i) * pl.GetPoint3dAt(i - 1).DistanceTo(nearStartPt) / pl.GetPoint3dAt(i).DistanceTo(nearStartPt),
        //                                pl.GetStartWidthAt(i - 1) + pl.GetEndWidthAt(i) * pl.GetPoint3dAt(i - 1).DistanceTo(nearStartPt) / pl.GetPoint3dAt(i).DistanceTo(nearStartPt));
        //        }


        //        if (pl.GetPoint3dAt(i) == newStartPt) startIndex = i;
        //        if (pl.GetPoint3dAt(i) == newEndPt) endIndex = i;
        //    }

        //    for (int i = 0; i <= endIndex - 1; i++)
        //    {
        //        truncPL.RemoveVertexAt(i);
        //    }

        //    for (int i = startIndex - 1; i >= 0; i--)
        //    {
        //        truncPL.RemoveVertexAt(i);
        //    }

        //    return truncPL;
        //}

        //[CommandMethod("HeadRow")]
        //public void ArrayBlocksAlongLength()
        //{
        //    //  Allows user to pick a source block
        //    //  Gather data from block attribute - spacing
        //    //  Use a dynamic streaching action to create a dynamically expanding array of this block
        //    //  If head block, bring CoverageArc with it and space according to settings
        //    //  Settings: 
        //    //      *Add each new head @== inserted Radius
        //    //      *Heads evenly spaced across entire array at 75%-90% of book radius
        //    //      *Angle of all heads ==
        //    //      *Angle of all heads Perpendicular to selected polyline


        //    // Select Block
        //    Document doc = acadApp.DocumentManager.MdiActiveDocument;
        //    Database db = doc.Database;
        //    Editor ed = doc.Editor;

        //    Transaction tr = db.TransactionManager.StartTransaction();

        //    using (tr)
        //    {
        //        BlockTable bt = (BlockTable)tr.GetObject(db.BlockTableId, OpenMode.ForRead);
        //        BlockTableRecord btr = (BlockTableRecord)tr.GetObject(bt[BlockTableRecord.ModelSpace], OpenMode.ForWrite);
        //        // Create block selector
        //        PromptEntityOptions peo = new PromptEntityOptions("\nSelect Source Block");
        //        peo.SetRejectMessage("\nThis is not a block.");
        //        peo.AddAllowedClass(typeof(BlockReference), false);

        //        PromptEntityResult per = ed.GetEntity(peo);
        //        if (per.Status == PromptStatus.OK)
        //        {
        //            BlockReference br = (BlockReference)tr.GetObject(per.ObjectId, OpenMode.ForRead);
        //            if (br.ObjectId != null)
        //            {
        //                double rowSpacing = 15; // To be replaced with radius value from head
        //                ObjectId covArcID = new ObjectId();

        //                // Start attribute support code
        //                // Add the attributes
        //                foreach (ObjectId attId in br.AttributeCollection)
        //                {
        //                    Entity ent = (Entity)tr.GetObject(attId, OpenMode.ForRead);

        //                    if (ent is AttributeReference)
        //                    {
        //                        AttributeReference ar = (AttributeReference)ent;
        //                        if (ar.Tag == "HS_SH_RADIUS") rowSpacing = StringToDouble(ar.TextString);
        //                        if (ar.Tag == "HS_SH_CHECKARCHANDLE") covArcID = db.GetObjectId(false, new Handle(Convert.ToInt64(StringToLong64Hex(ar.TextString))), 0);
        //                    } // end if
        //                } // end foreach                

        //                PromptPointOptions ppo = new PromptPointOptions("\nSelect End point.");
        //                ppo.BasePoint = br.Position;
        //                ppo.UseBasePoint = true;
        //                ppo.UseDashedLine = true;
        //                PromptPointResult ppr = ed.GetPoint(ppo);

        //                if (ppr.Status == PromptStatus.OK)
        //                {
        //                    //Choice structure. Space along rubberband to picked endpoint or a curve 
        //                    //Create our option menu?
        //                    PromptKeywordOptions pko = new PromptKeywordOptions("Trace Type.");
        //                    pko.AllowArbitraryInput = false;
        //                    pko.AllowNone = true;

        //                    // Add Options keyword
        //                    pko.Keywords.Add("PointToPoint");
        //                    pko.Keywords.Add("TraceCurve");
        //                    pko.Keywords.Default = "PointToPoint";

        //                    PromptResult pkr = ed.GetKeywords(pko);
        //                    if (pkr.Status != PromptStatus.OK) return;
        //                    if (pkr.StringResult == "TraceCurve")
        //                    {
        //                        peo.Message = "\nSelect Polyline or Line";
        //                        peo.SetRejectMessage("\nThis is not a Polyline or Line.");
        //                        peo.RemoveAllowedClass(typeof(BlockReference));
        //                        peo.AddAllowedClass(typeof(Line), false);
        //                        peo.AddAllowedClass(typeof(Polyline), false);

        //                        per = ed.GetEntity(peo);
        //                        if (per.Status == PromptStatus.OK)
        //                        {
        //                            DBObject obj = (DBObject)tr.GetObject(per.ObjectId, OpenMode.ForRead);

        //                            Line ln = obj as Line;
        //                            Polyline pl = obj as Polyline;

        //                            if (ln != null)
        //                            {
        //                                Point3d startPoint = ln.GetClosestPointTo(ppo.BasePoint, false);
        //                                Point3d endPoint = ln.GetClosestPointTo(ppr.Value, false);
        //                                // Now we have the data we need for projection along line segment
        //                                HeadRowLinear(tr, btr, br, startPoint, endPoint, rowSpacing, covArcID);
        //                            }
        //                            else if (pl != null)
        //                            {
        //                                Point3d startPoint = pl.GetClosestPointTo(ppo.BasePoint, false);
        //                                Point3d endPoint = pl.GetClosestPointTo(ppr.Value, false);
        //                                Polyline tracePL = TruncatePolyLine(pl, startPoint, endPoint, false);



        //                                //get collection of new head locations
        //                                Point3dCollection newHeadPts = new Point3dCollection();
        //                                Point3dCollection intersectPts = new Point3dCollection();
        //                                DoubleCollection newHeadAngles = new DoubleCollection();
        //                                Circle tempcir = new Circle();
        //                                tempcir.Radius = rowSpacing;
        //                                tempcir.Center = startPoint;
        //                                newHeadPts.Add(startPoint);
        //                                double remaingingArcLength = tempcir.Center.DistanceTo(endPoint);

        //                                while (remaingingArcLength >= rowSpacing)
        //                                {
        //                                    intersectPts.Clear();
        //                                    tracePL.IntersectWith(tempcir, Intersect.OnBothOperands, intersectPts, 0, 0);

        //                                    Point3d furtherIntersectPt = startPoint;
        //                                    foreach (Point3d intersectPt in intersectPts)
        //                                    {
        //                                        if (newHeadPts.Contains(intersectPt))
        //                                        {
        //                                            // already have that point ignore.
        //                                        }
        //                                        else
        //                                        {
        //                                            int newHeadPtsCount = newHeadPts.Count;

        //                                            if (GetDistanceToPoint(tracePL, intersectPt) > GetDistanceToPoint(tracePL, newHeadPts[newHeadPtsCount - 1])) furtherIntersectPt = intersectPt;

        //                                            double distToIntersect = GetDistanceToPoint(tracePL, intersectPt);
        //                                            Point3d newHeadPt = newHeadPts[newHeadPtsCount - 1];
        //                                            double distToNewHeadPt = GetDistanceToPoint(tracePL, newHeadPt);

        //                                            if (distToIntersect < distToNewHeadPt)
        //                                            {
        //                                                furtherIntersectPt = intersectPt;
        //                                            }
        //                                        }
        //                                    }
        //                                    // move circle to last intersection point and repeat

        //                                    tempcir.Center = furtherIntersectPt;
        //                                    if (remaingingArcLength > tempcir.Center.DistanceTo(endPoint))
        //                                    {
        //                                        newHeadPts.Add(furtherIntersectPt);
        //                                        remaingingArcLength = tempcir.Center.DistanceTo(endPoint);
        //                                    }
        //                                    else
        //                                    {
        //                                        ed.WriteMessage("\nSomething went wrong along the way. Aborted at point of bad data.");
        //                                        break;
        //                                    }

        //                                }
        //                                try { newHeadPts.Remove(startPoint); }
        //                                catch { }

        //                                //find angle in plane for each point on polyline
        //                                foreach (Point3d newHeadPt in newHeadPts)
        //                                {
        //                                    Vector3d uv = tracePL.GetFirstDerivative(newHeadPt) / tracePL.GetFirstDerivative(newHeadPt).Length;
        //                                    Vector3d x = Vector3d.XAxis;
        //                                    //double dotProd = uv.DotProduct(x);

        //                                    // The rotation problem is somewhere here. Maybe a xproduct could help?

        //                                    double theta = uv.GetAngleTo(x);

        //                                    newHeadAngles.Add(theta - br.Rotation + br.Rotation > Math.PI ? Math.PI : 0);
        //                                    //double theta = Math.Acos(dotProd);

        //                                    //newHeadAngles.Add(theta - br.Rotation); 
        //                                }

        //                                // now add the heads
        //                                btr.UpgradeOpen();

        //                                for (int i = 0; i < newHeadPts.Count; i++)
        //                                {
        //                                    Matrix3d tmatrix = Matrix3d.Displacement(br.Position.GetVectorTo(newHeadPts[i]));
        //                                    Matrix3d rmatrix = Matrix3d.Rotation(newHeadAngles[i], Vector3d.ZAxis, newHeadPts[i]);

        //                                    BlockReference newHead = (BlockReference)br.Clone();
        //                                    Polyline newCovArc = (Polyline)tr.GetObject(covArcID, OpenMode.ForRead).Clone();

        //                                    newCovArc.TransformBy(tmatrix);
        //                                    newCovArc.TransformBy(rmatrix);
        //                                    btr.AppendEntity(newCovArc);
        //                                    tr.AddNewlyCreatedDBObject(newCovArc, true);

        //                                    // add the newCovArc Handle to newHead attribute. -- Not working, adds same handle as origional covArc
        //                                    foreach (AttributeReference ar in newHead.AttributeCollection)
        //                                    {
        //                                        if (ar.Tag == "HS_SH_CHECKARCHHANDLE") ar.TextString = newCovArc.Handle.ToString();
        //                                    }
        //                                    newHead.TransformBy(tmatrix);
        //                                    newHead.TransformBy(rmatrix);
        //                                    btr.AppendEntity(newHead);
        //                                    tr.AddNewlyCreatedDBObject(newHead, true);

        //                                }
        //                                btr.DowngradeOpen();

        //                            }
        //                            else //was not an allowed class.  Shouldn't happen.
        //                            {
        //                                ed.WriteMessage("\nNot allowed class. Aborting.");
        //                                return;
        //                            }
        //                        }
        //                    }
        //                    else //pkr.StringResult == "PointToPoint"
        //                    {
        //                        Point3d startPoint = ppo.BasePoint;
        //                        Point3d endPoint = ppr.Value;
        //                        // Now we have the data we need for projection along line segment
        //                        HeadRowLinear(tr, btr, br, startPoint, endPoint, rowSpacing, covArcID);
        //                    }
        //                }
        //            }
        //        }

        //        tr.Commit();
        //    }
        //}

        //public static double GetDistanceToPoint(Curve curve, Point3d pt)
        //{
        //    Point3d ptOnCurve = curve.GetClosestPointTo(pt, false);
        //    double ptparam = curve.GetParameterAtPoint(ptOnCurve);
        //    double a = curve.GetDistanceAtParameter(ptparam);
        //    double b = curve.GetDistanceAtParameter(curve.StartParam);
        //    return a - b;
        //}

        //private static bool IsCollinear(Line AB, Line CD)
        //{
        //    double deltaACy = AB.StartPoint.Y - CD.StartPoint.Y;
        //    double deltaDCx = CD.EndPoint.X - CD.StartPoint.X;
        //    double deltaACx = AB.StartPoint.X - CD.StartPoint.X;
        //    double deltaDCy = CD.EndPoint.Y - CD.StartPoint.Y;
        //    double deltaBAx = AB.EndPoint.X - AB.StartPoint.X;
        //    double deltaBAy = AB.EndPoint.Y - AB.StartPoint.Y;

        //    double denominator = deltaBAx * deltaDCy - deltaBAy * deltaDCx;
        //    double numerator = deltaACy * deltaDCx - deltaACx * deltaDCy;

        //    if (numerator == 0 && denominator == 0) return true;
        //    else return false;
        //}

        //private static double StringToDouble(string st)
        //{
        //    double temp;
        //    if (st == "") return -1;
        //    else
        //    {
        //        try
        //        {
        //            temp = Convert.ToDouble(st);
        //            return temp;
        //        }
        //        catch (Autodesk.AutoCAD.Runtime.Exception)
        //        {
        //            return -1;
        //        }
        //        catch (System.Exception)
        //        {
        //            return -1;
        //        }
        //    }
        //}

        //private static double StringToLong64Hex(string st)
        //{
        //    Int64 temp;
        //    if (st == "") return -1;
        //    else
        //    {
        //        try
        //        {
        //            temp = Convert.ToInt64(st, 16);
        //            return temp;
        //        }
        //        catch (Autodesk.AutoCAD.Runtime.Exception)
        //        {
        //            return -1;
        //        }
        //        catch (System.Exception)
        //        {
        //            return -1;
        //        }
        //    }
        //}

        //private static Point3d ClosestPointOnCurve(Curve curve, Point3d pt)
        //{
        //    Point3d pNearest = new Point3d();
        //    Point3dCollection plPoints = new Point3dCollection();
        //    Vector3dCollection plVectors = new Vector3dCollection();

        //    if (curve != null)
        //    {
        //        Point3dCollection nearPointCol = new Point3dCollection();
        //        ObjectId objId = curve.ObjectId;

        //        Line ln = curve as Line;
        //        Polyline pl = curve as Polyline;
        //        if (ln != null)
        //        {
        //            //is line                    
        //            ClosestPtOnLinearSegment(pt, pNearest, ln);
        //        }
        //        else if (pl != null)
        //        {
        //            // cycle through each segment

        //            Point3dCollection pNearestCol = new Point3dCollection();
        //            for (int i = 0; i < pl.NumberOfVertices - 1; i++)
        //            {
        //                if (pl.GetBulgeAt(i) != 0)
        //                {
        //                    Arc ars = new Arc(pl.GetArcSegmentAt(i).Center,
        //                                      pl.GetArcSegmentAt(i).Radius,
        //                                      pl.GetArcSegmentAt(i).StartAngle,
        //                                      pl.GetArcSegmentAt(i).EndAngle);
        //                    //ars.StartPoint = pl.GetArcSegmentAt(i).StartPoint;
        //                    //ars.EndPoint = pl.GetArcSegmentAt(i).EndPoint;
        //                    ClosestPtOnArcSegment(pt, pNearest, ars);

        //                }
        //                else
        //                {
        //                    Line ls = new Line(pl.GetLineSegmentAt(i).StartPoint, pl.GetLineSegmentAt(i).EndPoint);
        //                    ClosestPtOnLinearSegment(pt, pNearest, ls);
        //                }
        //            }
        //        }
        //    }
        //    return pNearest;
        //}

        //private static Point3d ClosestPtOnArcSegment(/*ref*/ Point3d pt, Point3d pNearest, Arc ars)
        //{
        //    Point3d startPt = ars.StartPoint;
        //    Point3d endPt = ars.EndPoint;
        //    Point3d ctrPt = ars.Center;

        //    Line dummyLine = new Line(pt, ctrPt);

        //    Point3dCollection intersects = new Point3dCollection();
        //    // if the dummyLine intersects the line between start & end, the nearest point is that intersection, else it is the shorter of the two.
        //    dummyLine.IntersectWith(ars, Intersect.OnBothOperands, intersects, 0, 0);

        //    if (intersects.Count == 1) pNearest = intersects[0];
        //    else if (intersects.Count == 0) pNearest = pt.DistanceTo(startPt) > pt.DistanceTo(endPt) ? endPt : startPt;
        //    else
        //    {
        //        // shouldn't be possible                        
        //    }
        //    return pNearest;
        //}

        //private static Point3d ClosestPtOnLinearSegment(/*ref*/ Point3d pt, Point3d pNearest, Line ln)
        //{
        //    Point3d startPt = ln.StartPoint;
        //    Point3d endPt = ln.EndPoint;
        //    Vector3d vect = ln.StartPoint.GetVectorTo(ln.EndPoint);

        //    // find unit vector normal to the line segment
        //    Vector3d normal = vect.GetNormal();
        //    Vector3d unormal = normal / normal.Length;

        //    double dummyDist = Math.Max(pt.DistanceTo(startPt), pt.DistanceTo(endPt));
        //    Line dummyLine = new Line(pt, pt.Add(unormal.MultiplyBy(dummyDist)));

        //    Point3dCollection intersects = new Point3dCollection();
        //    // if the dummyLine intersects the line between start & end, the nearest point is that intersection, else it is the shorter of the two.
        //    dummyLine.IntersectWith(ln, Intersect.OnBothOperands, intersects, 0, 0);

        //    if (intersects.Count == 1) pNearest = intersects[0];
        //    else if (intersects.Count == 0) pNearest = pt.DistanceTo(startPt) > pt.DistanceTo(endPt) ? endPt : startPt;
        //    else
        //    {
        //        // shouldn't be possible                        
        //    }
        //    return pNearest;
        //}

        //private static Point3d PointIntersectsCurve(Point3dCollection plPoints, Vector3dCollection plVectors, Curve cv, Point3d endPt)
        //{
        //    Point3d pNearest = new Point3d();
        //    Double minMagnitude = new Double();
        //    Point3d pSelected = endPt;

        //    for (int i = 0; i <= plPoints.Count - 1; i++)
        //    {
        //        Vector3d P0toP1 = new Vector3d();
        //        Vector3d P0toP2 = new Vector3d();
        //        Vector3d P1toP2 = new Vector3d();
        //        P0toP1 = pSelected.GetVectorTo(plPoints[i]);
        //        P1toP2 = plVectors[i];
        //        Double curMagnitude = new Double();
        //        Point3d curPoint = new Point3d();
        //        if (cv.Closed && (i <= plPoints.Count - 2))
        //        {
        //            P0toP2 = pSelected.GetVectorTo(plPoints[i + 1]);
        //            if (P1toP2.DotProduct(P0toP1) * P1toP2.DotProduct(P0toP2) >= 0)
        //            {
        //                if (P0toP1.Length <= P0toP2.Length)
        //                {
        //                    curMagnitude = P0toP1.Length;
        //                    curPoint = plPoints[i];
        //                }
        //                else
        //                {
        //                    curMagnitude = P0toP2.Length;
        //                    curPoint = plPoints[i + 1];
        //                }
        //            }
        //            else if (P1toP2.DotProduct(P0toP1) * P1toP2.DotProduct(P0toP2) < 0)
        //            {
        //                curMagnitude = Math.Abs(P0toP1.CrossProduct(P0toP2).Length) / Math.Abs(P1toP2.Length);
        //                curPoint = plPoints[i].Add(Math.Sqrt(P0toP1.LengthSqrd - Math.Pow(curMagnitude, 2)) * P1toP2.DivideBy(P1toP2.Length));
        //            }
        //        }
        //        else if (cv.Closed && (i <= plPoints.Count - 1))
        //        {
        //            P0toP2 = pSelected.GetVectorTo(plPoints[0]);
        //            if (P1toP2.DotProduct(P0toP1) * P1toP2.DotProduct(P0toP2) >= 0)
        //            {
        //                if (P0toP1.Length <= P0toP2.Length)
        //                {
        //                    curMagnitude = P0toP1.Length;
        //                    curPoint = plPoints[i];
        //                }
        //                else
        //                {
        //                    curMagnitude = P0toP2.Length;
        //                    curPoint = plPoints[0];
        //                }
        //            }
        //            else if (P1toP2.DotProduct(P0toP1) * P1toP2.DotProduct(P0toP2) < 0)
        //            {
        //                curMagnitude = Math.Abs(P0toP1.CrossProduct(P0toP2).Length) / Math.Abs(P1toP2.Length);
        //                curPoint = plPoints[i].Add(Math.Sqrt(P0toP1.LengthSqrd - Math.Pow(curMagnitude, 2)) * P1toP2.DivideBy(P1toP2.Length));
        //            }
        //        }
        //        else if (!cv.Closed && (i <= plPoints.Count - 2))
        //        {
        //            P0toP2 = pSelected.GetVectorTo(plPoints[i + 1]);
        //            if (P1toP2.DotProduct(P0toP1) * P1toP2.DotProduct(P0toP2) >= 0)
        //            {
        //                if (P0toP1.Length <= P0toP2.Length)
        //                {
        //                    curMagnitude = P0toP1.Length;
        //                    curPoint = plPoints[i];
        //                }
        //                else
        //                {
        //                    curMagnitude = P0toP2.Length;
        //                    curPoint = plPoints[i + 1];
        //                }
        //            }
        //            else if (P1toP2.DotProduct(P0toP1) * P1toP2.DotProduct(P0toP2) < 0)
        //            {
        //                curMagnitude = Math.Abs(P0toP1.CrossProduct(P0toP2).Length) / Math.Abs(P1toP2.Length);
        //                curPoint = plPoints[i].Add(Math.Sqrt(P0toP1.LengthSqrd - Math.Pow(curMagnitude, 2)) * P1toP2.DivideBy(P1toP2.Length));
        //            }
        //        }
        //        else if (!cv.Closed && (i == plPoints.Count - 1))
        //        {
        //            // end of line reached, reverse.
        //            P0toP2 = pSelected.GetVectorTo(plPoints[i - 1]);
        //            if (P1toP2.DotProduct(P0toP1) * P1toP2.DotProduct(P0toP2) >= 0)
        //            {
        //                if (P0toP1.Length <= P0toP2.Length)
        //                {
        //                    curMagnitude = P0toP1.Length;
        //                    curPoint = plPoints[i];
        //                }
        //                else
        //                {
        //                    curMagnitude = P0toP2.Length;
        //                    curPoint = plPoints[i - 1];
        //                }
        //            }
        //            else if (P1toP2.DotProduct(P0toP1) * P1toP2.DotProduct(P0toP2) < 0)
        //            {
        //                curMagnitude = Math.Abs(P0toP1.CrossProduct(P0toP2).Length) / Math.Abs(P1toP2.Length);
        //                curPoint = plPoints[i].Add(Math.Sqrt(P0toP1.LengthSqrd - Math.Pow(curMagnitude, 2)) * P1toP2.DivideBy(P1toP2.Length));
        //            }
        //        }

        //        if (i == 0 || curMagnitude <= minMagnitude)
        //        {
        //            minMagnitude = curMagnitude;
        //            pNearest = curPoint;
        //        }                            
        //    }
        //    return pNearest;
        //}


        //private static void HeadRowLinear(Transaction tr, BlockTableRecord btr, BlockReference br, Point3d startPoint, Point3d endPoint, double rowSpacing, ObjectId covArcID)
        //{
        //    double distance = startPoint.DistanceTo(endPoint);
        //    Vector3d vect = startPoint.GetVectorTo(endPoint) / distance * rowSpacing;
        //    int numberOfRows = (int)(distance / rowSpacing);
        //    if (numberOfRows > 0)
        //    {
        //        for (int i = 1; i <= numberOfRows; i++)
        //        {
        //            Matrix3d tmatrix = Matrix3d.Displacement(vect * i);
        //            BlockReference newHead = (BlockReference)br.Clone();
        //            Polyline newCovArc = (Polyline)tr.GetObject(covArcID, OpenMode.ForRead).Clone();

        //            newCovArc.TransformBy(tmatrix);
        //            btr.AppendEntity(newCovArc);
        //            tr.AddNewlyCreatedDBObject(newCovArc, true);



        //            // add the newCovArc Handle to newHead attribute. -- Not working, adds same handle as origional covArc
        //            foreach (AttributeReference ar in newHead.AttributeCollection)
        //            {
        //                if (ar.Tag == "HS_SH_CHECKARCHANDLE")
        //                {
        //                    ar.TextString = newCovArc.Handle.ToString();
        //                }
        //            }

        //            newHead.TransformBy(tmatrix);
        //            btr.AppendEntity(newHead);
        //            tr.AddNewlyCreatedDBObject(newHead, true);


        //        }
        //        btr.DowngradeOpen();
        //    }
        //}

        //private static void HighlightEntities(ObjectIdCollection objIds)
        //{
        //    Document doc = acadApp.DocumentManager.MdiActiveDocument;
        //    Transaction tr = doc.TransactionManager.StartTransaction();

        //    using (tr)
        //    {
        //        foreach (ObjectId objId in objIds)
        //        {
        //            Entity ent = tr.GetObject(objId, OpenMode.ForRead) as Entity;
        //            if (ent != null) ent.Highlight();
        //        }
        //    }
        //}
        #endregion

        #region GetBlockCenter
        [CommandMethod("BCP")]
        public void GetBlockCenter()
        {
            Document doc = acadApp.DocumentManager.MdiActiveDocument;
            Editor ed = doc.Editor;
            Database db = doc.Database;

            Point3d pt = BlockCenterPt();
            
            using (Transaction tr = db.TransactionManager.StartTransaction())
            { 
                try
                {
                    BlockTable bt = (BlockTable)tr.GetObject(doc.Database.BlockTableId, OpenMode.ForRead);
                    BlockTableRecord msbtr = (BlockTableRecord)tr.GetObject(bt[BlockTableRecord.ModelSpace], OpenMode.ForRead);
                    
                    DBPoint dbPt = new DBPoint(pt);

                    msbtr.UpgradeOpen();
                    msbtr.AppendEntity(dbPt);
                    tr.AddNewlyCreatedDBObject(dbPt, true);
                    msbtr.DowngradeOpen();
                }
                catch (Autodesk.AutoCAD.Runtime.Exception ex)
                {
                    ed.WriteMessage("\nError. {0}", ex.Message);
                }
                
                tr.Commit();   
            }                     
        }


        public Point3d BlockCenterPt()
        {
            Document doc = acadApp.DocumentManager.MdiActiveDocument;
            Editor ed = doc.Editor;
            Database db = doc.Database;
            Transaction tr = db.TransactionManager.StartTransaction();

            Point3d pt = new Point3d();
            // Start the transaction
            try
            {
                BlockTable bt = (BlockTable)tr.GetObject(doc.Database.BlockTableId, OpenMode.ForRead);                

                PromptEntityOptions peo = new PromptEntityOptions("\nSelect Block. ");
                peo.SetRejectMessage("Not a block. Try again.");
                peo.AddAllowedClass(typeof(BlockReference), false);
                peo.AllowObjectOnLockedLayer = false;
                PromptEntityResult per = ed.GetEntity(peo);

                if (per.Status == PromptStatus.OK)
                {
                    BlockReference br = (BlockReference)tr.GetObject(per.ObjectId, OpenMode.ForRead);
                    DBObjectCollection objs = new DBObjectCollection();                                        
                    
                    br.Explode(objs);
                    
                    foreach (DBObject obj in objs)
                    {
                        if (obj.GetType() == typeof(DBPoint))
                        {                            
                            pt = obj.Bounds.Value.MinPoint;
                            break;
                        }                        
                    }
                }                                
                tr.Commit();
            }
            catch (Autodesk.AutoCAD.Runtime.Exception ex)
            {
                ed.WriteMessage("\nError. {0}", ex.Message);
            }
            return pt;
        }

        public Point3d BlockCenterPt(BlockReference br)
        {
            Document doc = acadApp.DocumentManager.MdiActiveDocument;
            Editor ed = doc.Editor;
            Database db = doc.Database;
            Transaction tr = db.TransactionManager.StartTransaction();

            Point3d pt = new Point3d();
            
            try
            {
                BlockTable bt = (BlockTable)tr.GetObject(doc.Database.BlockTableId, OpenMode.ForRead);
                DBObjectCollection objs = new DBObjectCollection();                                        
                    
                br.Explode(objs);
                    
                foreach (DBObject obj in objs)
                {
                    if (obj.GetType() == typeof(DBPoint))
                    {                            
                        pt = obj.Bounds.Value.MinPoint;
                        break;
                    }                        
                }
                tr.Commit();
            }
            catch (Autodesk.AutoCAD.Runtime.Exception ex)
            {
                ed.WriteMessage("\nError. [0]", ex.Message);
            }
            
            return pt;
        }

        #endregion

        #region BreakLateralAtHead

        [CommandMethod("BreakLateralAtHead")] //Copy to BreakLateralAtValve
        public void BreakLateralAtHead()
        {
            Document doc = acadApp.DocumentManager.MdiActiveDocument;
            Editor ed = doc.Editor;
            Database db = doc.Database;
            Transaction tr = db.TransactionManager.StartTransaction();
            try
            {
                BlockTable bt = (BlockTable)tr.GetObject(doc.Database.BlockTableId, OpenMode.ForRead);
                BlockTableRecord ms = (BlockTableRecord)tr.GetObject(bt[BlockTableRecord.ModelSpace], OpenMode.ForRead);
                                
                PromptEntityOptions peo = new PromptEntityOptions("\nSelect Head. ");
                peo.SetRejectMessage("\nNot a head. Try again.");
                peo.AddAllowedClass(typeof(BlockReference), false);
                peo.AllowObjectOnLockedLayer = false;                
                PromptEntityResult per = ed.GetEntity(peo);

                if (per.Status == PromptStatus.OK)
                {
                    BlockReference br = (BlockReference)tr.GetObject(per.ObjectId, OpenMode.ForRead);
                    if (br.Name.StartsWith("SH-"))
                    {
                        BreakLateralAtBlock(br, IrrigationObjectType.SprayHead);
                    }
                }
            }
            catch (Autodesk.AutoCAD.Runtime.Exception ex)
            {
                ed.WriteMessage("\nError. [0]", ex.Message);
            } 
            tr.Commit();
        }

        public void BreakLateralAtBlock(BlockReference br, IrrigationObjectType irrObjType)
        {
            Document doc = acadApp.DocumentManager.MdiActiveDocument;
            Editor ed = doc.Editor;
            Database db = doc.Database;
            Transaction tr = db.TransactionManager.StartTransaction();

            //Point3d pt = new Point3d();

            // Start the transaction
            try
            {
                BlockTable bt = (BlockTable)tr.GetObject(doc.Database.BlockTableId, OpenMode.ForRead);
                BlockTableRecord ms = (BlockTableRecord)tr.GetObject(bt[BlockTableRecord.ModelSpace], OpenMode.ForRead);
                                
                PromptEntityOptions peo = new PromptEntityOptions("\nSelect Lateral. ");
                peo.SetRejectMessage("Not a polyline. Try again.");
                peo.AddAllowedClass(typeof(Polyline), false);
                peo.AllowObjectOnLockedLayer = false;
                PromptEntityResult per = ed.GetEntity(peo);

                if (per.Status == PromptStatus.OK)
                {
                    Polyline pl = (Polyline)tr.GetObject(per.ObjectId, OpenMode.ForRead);
                    Point3d centerPt = BlockCenterPt(br);                    
                    switch(irrObjType)
                    {
                        case IrrigationObjectType.Valve:             
                            //
                            // Call function to trim deadend
                            break;
                        case IrrigationObjectType.SprayHead:
                            // if lateral does not exit, trim to nearest point.
                            break;
                        default:
                            break;
                    }
                                        
                    
                    // Does lateral touch block center? If not, make it.
                    // If it's a valve, it can only enter. If it's a head:
                    // Does lateral enter & exit ? If not, we have a spare vertex...
                    // Divide into two polylines at head center.
                    // Update database.
                    // Down the line -- once everything is connected -- we need to make sure we don't have a dead end. Maybe that should be in the zone setup function.
                }
                                
                tr.Commit();
            }
            catch (Autodesk.AutoCAD.Runtime.Exception ex)
            {
                ed.WriteMessage("\nError. [0]", ex.Message);
            }         
        }        
        #endregion

        #region CombineBlocksIntoLibrary

        //[CommandMethod("CombineBlocksIntoLibrary")]
        //public void CombineBlocksIntoLibrary()
        //{
        //    Document doc = acadApp.DocumentManager.MdiActiveDocument;
        //    Editor ed = doc.Editor;
        //    Database destDb = doc.Database;

        //    // Get name of folder from which to load and import blocks
        //    PromptResult pr = ed.GetString("\nEnter the folder of source drawings: ");

        //    if (pr.Status != PromptStatus.OK) return;

        //    string pathName = pr.StringResult;

        //    // Check the folder exists
        //    if (!Directory.Exists(pathName))
        //    {
        //        ed.WriteMessage("\nDirectory does not exist: {0}", pathName);
        //        return;
        //    }

        //    // Get the names of our DWG files in that folder
        //    string[] fileNames = Directory.GetFiles(pathName, "*.dwg");

        //    // A counter for the files we've imported
        //    int imported = 0, failed = 0;

        //    // For each file in our list
        //    foreach (string fileName in fileNames)
        //    {
        //        // Double-check we have a DWG file (probably unnecessary)
        //        if (fileName.EndsWith(".dwg", StringComparison.InvariantCultureIgnoreCase))
        //        {
        //            // Catch exceptions at the file level to allow skipping
        //            try
        //            {
        //                // Suggestion from Thorsten Meinecke...
        //                string destName = SymbolUtilityServices.GetSymbolNameFromPathName(fileName, "dwg");
        //                // And from Dan Glassman...
        //                destName = SymbolUtilityServices.RepairSymbolName(destName, false);

        //                // Create a source database to load the DWG into
        //                using (Database db = new Database(false, true))
        //                {
        //                    // Read the DWG into our side database
        //                    db.ReadDwgFile(fileName, FileShare.Read, true, "");

        //                    //command not exposed, defaulting to false
        //                    //bool isAnno = db.AnnotativeDwg;                            
        //                    bool isAnno = db.Cannoscale.Scale >= 1 ? true : false;

        //                    // Insert it into the destination database as
        //                    // a named block definition
        //                    ObjectId btrId = destDb.Insert(destName, db, false);

        //                    if (isAnno)
        //                    {
        //                        // If an annotative block, open the resultant BTR
        //                        // and set its annotative definition status
        //                        Transaction tr = destDb.TransactionManager.StartTransaction();
        //                        using (tr)
        //                        {
        //                            BlockTableRecord btr = (BlockTableRecord)tr.GetObject(btrId, OpenMode.ForWrite);
        //                            btr.Annotative = AnnotativeStates.True;
        //                            tr.Commit();
        //                        }
        //                    }

        //                    // Print message and increment imported block counter
        //                    ed.WriteMessage("\nImported from \"{0}\".", fileName);
        //                    imported++;
        //                }
        //            }
        //            catch (System.Exception ex)
        //            {
        //                ed.WriteMessage("\nProblem importing \"{0}\": {1} - file skipped.", fileName, ex.Message);
        //                failed++;
        //            }
        //        }
        //    }

        //    ed.WriteMessage("\nImported block definitions from {0} files{1} in \"{2}\" into the current drawing.", imported, failed > 0 ? " (" + failed + " failed)" : "", pathName);
        //}

        #endregion

        #region OrderObjects
        /// <summary>
        /// Reorders text and non-xref blocks and places them on top in drawing
        /// Autocad 2008x64 - SP1 Tested Successfully 3/16/10
        /// Autocad 2007x64 - SP2 Tested Successfully 3/16/10
        /// </summary>
        //[CommandMethod("OrderObjects", CommandFlags.UsePickSet | CommandFlags.Redraw | CommandFlags.Modal)]
        //static public void OrderObjects()
        //{
        //    Document doc = acadApp.DocumentManager.MdiActiveDocument;
        //    Editor ed = acadApp.DocumentManager.MdiActiveDocument.Editor;
        //    Database db = HostApplicationServices.WorkingDatabase;
        //    Transaction tr = db.TransactionManager.StartTransaction();

        //    // Start the transaction
        //    try
        //    {
        //        // Build a filter list so that only
        //        // block references, & Text are selected
        //        TypedValue[] filList = new TypedValue[] { new TypedValue((int)DxfCode.Start, "INSERT") };

        //        SelectionFilter filter = new SelectionFilter(filList);

        //        PromptSelectionOptions opts = new PromptSelectionOptions();
        //        opts.MessageForAdding = "Select Blocks to bring to top of draw order: ";

        //        PromptSelectionResult res = ed.SelectImplied();

        //        string user = System.Windows.Forms.SystemInformation.UserName.Remove(System.Windows.Forms.SystemInformation.UserName.Length - 2).ToUpperInvariant();
        //        string drink = "";
        //        switch (user)
        //        {
        //            case "KEN":
        //                drink = "Go get some decaf";
        //                break;
        //            case "DOROTHY":
        //                drink = "Go get some tea";
        //                break;
        //            case "ISRAEL":
        //                drink = "Go get some tea";
        //                break;
        //            case "JIM":
        //                drink = "Mutter to yourself for a while";
        //                break;
        //            default:
        //                drink = "Go get some coffee";
        //                break;
        //        }
        //        ed.WriteMessage("Ordering Objects.  On large drawings, this may take a while.  {0} {1}.", drink, user);
        //        // If there's no pickfirst set available...
        //        if (res.Status == PromptStatus.Error)
        //        {
        //            // ... ask the user to select entities            

        //            res = ed.GetSelection(opts, filter);
        //        }
        //        else
        //        {
        //            // If there was a pickfirst set, clear it                    
        //            ed.SetImpliedSelection(new ObjectId[0]);
        //        }

        //        ObjectIdCollection idCol = new ObjectIdCollection();

        //        if (res.Status == PromptStatus.OK)
        //        {

        //            idCol = ObjIdArrayToCollection(res.Value.GetObjectIds());
        //            ObjectIdCollection xrCol = FindXrefsInBlockRefCollection(doc, tr, ref idCol);

        //            for (int i = idCol.Count - 1; i > 0; i--)
        //            {
        //                Entity ent = tr.GetObject(idCol[i], OpenMode.ForRead) as Entity;
        //                if (!((ent as DBText) != null && (ent as MText) != null && (ent as BlockReference) != null)) idCol.Remove(idCol[i]);
        //            }

        //            foreach (ObjectId id in idCol)
        //            {
        //                SelectedObject so = new SelectedObject(id, SelectionMethod.NonGraphical, -1);
        //                if (so != null) idCol.Remove(id);
        //            }


        //            foreach (ObjectId id in idCol)
        //            {
        //                try
        //                {
        //                    if ((Entity)id.GetObject(OpenMode.ForRead) != null)
        //                    {

        //                    }
        //                }
        //                catch
        //                {
        //                    idCol.Remove(id);
        //                }

        //            }

        //            if (idCol.Count > 0) HighlightEntities(idCol);
        //            using (tr)
        //            {
        //                BlockTable bt = (BlockTable)tr.GetObject(doc.Database.BlockTableId, OpenMode.ForRead);
        //                BlockTableRecord btr = (BlockTableRecord)tr.GetObject(bt[BlockTableRecord.ModelSpace], OpenMode.ForWrite);

        //                //ObjectIdCollection ids = new ObjectIdCollection();
        //                //foreach (ObjectId entId in idCol)
        //                //{ 
        //                //    ids.Add(entId);                            
        //                //}
        //                DrawOrderTable dot = (DrawOrderTable)tr.GetObject(btr.DrawOrderTableId, OpenMode.ForWrite);
        //                if (idCol.Count > 0)
        //                {
        //                    foreach (ObjectId id in idCol)
        //                    {
        //                        ObjectIdCollection ids = new ObjectIdCollection();
        //                        ids.Add(id);
        //                        try
        //                        {
        //                            dot.MoveToTop(ids);
        //                            idCol.Remove(id);
        //                        }
        //                        catch (Autodesk.AutoCAD.Runtime.Exception)
        //                        {

        //                        }
        //                    }
        //                    ed.WriteMessage("\nBlocks & Text moved to top.");
        //                }
        //                if (xrCol.Count > 0)
        //                {
        //                    dot.MoveToBottom(xrCol);
        //                    ed.WriteMessage("\nSelected Xrefs moved to bottom.");
        //                }
        //                ed.Regen();
        //                tr.Commit();
        //            }
        //        }
        //    }
        //    catch (Autodesk.AutoCAD.Runtime.Exception ex)
        //    {
        //        ed.WriteMessage("Exception: " + ex.Message + "\n" + ex.InnerException + "\n" + ex.Source + "\n" + ex.Data + "\n" + ex.ErrorStatus + "\n" + ex.StackTrace + "\n" + ex.TargetSite);
        //    }
        //    finally
        //    {
        //        tr.Dispose();
        //    }

        //}

        //private static ObjectIdCollection ObjIdArrayToCollection(ObjectId[] idArray)
        //{
        //    ObjectIdCollection idCol = new ObjectIdCollection();
        //    foreach (ObjectId id in idArray)
        //    {
        //        idCol.Add(id);
        //    }
        //    return idCol;
        //}

        //private static ObjectIdCollection FindXrefsInBlockRefCollection(Document doc, Transaction tr, ref ObjectIdCollection idCol)
        //{
        //    Database db = HostApplicationServices.WorkingDatabase;
        //    ObjectIdCollection xrCol = new ObjectIdCollection();
        //    XrefGraph xrgraph = doc.Database.GetHostDwgXrefGraph(false);
        //    // look at all Nodes in the XrefGraph.  Skip 0 node since it is the drawing itself.
        //    for (int i = 1; i < (xrgraph.NumNodes - 1); i++)
        //    {
        //        XrefGraphNode xrNode = xrgraph.GetXrefNode(i);

        //        BlockTableRecord btr = (BlockTableRecord)tr.GetObject
        //            (xrNode.BlockTableRecordId, OpenMode.ForWrite);

        //        switch (xrNode.XrefStatus)
        //        {
        //            //if it is a resolved xref, then add to collection to be found
        //            case Autodesk.AutoCAD.DatabaseServices.XrefStatus.Resolved:
        //                xrCol.Add(btr.ObjectId);
        //                break;
        //            case Autodesk.AutoCAD.DatabaseServices.XrefStatus.Unloaded:

        //                break;
        //            case Autodesk.AutoCAD.DatabaseServices.XrefStatus.FileNotFound:

        //                break;
        //            case Autodesk.AutoCAD.DatabaseServices.XrefStatus.NotAnXref:

        //                break;
        //            case Autodesk.AutoCAD.DatabaseServices.XrefStatus.Unreferenced:

        //                break;
        //        }
        //    }

        //    // remove the xrefs from the block/text collection            
        //    foreach (ObjectId entId in idCol)
        //    {
        //        foreach (ObjectId xid in xrCol)
        //        {
        //            if (entId == xid) idCol.Remove(entId);
        //        }
        //    }
        //    return xrCol;
        //}
        #endregion

        #region TranslationTypical
        //[CommandMethod("TRANS", CommandFlags.UsePickSet)]
        //static public void TransformEntity()
        //{
        //    Document doc = acadApp.DocumentManager.MdiActiveDocument;
        //    Database db = doc.Database;
        //    Editor ed = doc.Editor;

        //    // Our selected entity (only one supported, for now)
        //    ObjectId id;

        //    // First query the pickfirst selection set
        //    PromptSelectionResult psr = ed.SelectImplied();
        //    if (psr.Status != PromptStatus.OK || psr.Value == null)
        //    {
        //        // If nothing selected, ask the user

        //        PromptEntityOptions peo = new PromptEntityOptions("\nSelect entity to transform: ");
        //        PromptEntityResult per = ed.GetEntity(peo);
        //        if (per.Status != PromptStatus.OK) return;
        //        id = per.ObjectId;
        //    }
        //    else
        //    {
        //        // If the pickfirst set has one entry, take it
        //        SelectionSet ss = psr.Value;
        //        if (ss.Count != 1)
        //        {
        //            ed.WriteMessage("\nThis command works on a single entity.");
        //            return;
        //        }
        //        ObjectId[] ids = ss.GetObjectIds();
        //        id = ids[0];
        //    }

        //    PromptResult pr = ed.GetString("\nEnter property name: ");
        //    if (pr.Status != PromptStatus.OK) return;

        //    string prop = pr.StringResult;
        //    // Now let's ask for the matrix string
        //    pr = ed.GetString("\nEnter matrix values (Comma seperated, 4x4): ");
        //    if (pr.Status != PromptStatus.OK) return;

        //    // Split the string into its individual cells
        //    string[] cells = pr.StringResult.Split(new char[] { ',' });
        //    if (cells.Length != 16)
        //    {
        //        ed.WriteMessage("\nMust contain 16 entries.");
        //        return;
        //    }

        //    try
        //    {
        //        // Convert the array of strings into one of doubles

        //        double[] data = new double[cells.Length];
        //        for (int i = 0; i < cells.Length; i++)
        //        {
        //            data[i] = double.Parse(cells[i]);
        //        }

        //        // Create a 3D matrix from our cell data
        //        Matrix3d mat = new Matrix3d(data);

        //        // Now we can transform the selected entity

        //        Transaction tr = doc.TransactionManager.StartTransaction();
        //        using (tr)
        //        {
        //            Entity ent = tr.GetObject(id, OpenMode.ForWrite) as Entity;
        //            if (ent != null)
        //            {
        //                bool transformed = false;
        //                // If the user specified a property to modify

        //                if (!string.IsNullOrEmpty(prop))
        //                {
        //                    // Query the property's value

        //                    object val = ent.GetType().InvokeMember(prop, BindingFlags.GetProperty, null, ent, null);

        //                    // We only know how to transform points and vectors
        //                    if (val is Point3d)
        //                    {
        //                        // Cast and transform the point result
        //                        Point3d pt = (Point3d)val, res = pt.TransformBy(mat);

        //                        // Set it back on the selected object
        //                        ent.GetType().InvokeMember(prop, BindingFlags.SetProperty, null, ent, new object[] { res });
        //                        transformed = true;
        //                    }
        //                    else if (val is Vector3d)
        //                    {
        //                        // Cast and transform the vector result

        //                        Vector3d vec = (Vector3d)val, res = vec.TransformBy(mat);
        //                        // Set it back on the selected object
        //                        ent.GetType().InvokeMember(prop, BindingFlags.SetProperty, null, ent, new object[] { res });
        //                        transformed = true;
        //                    }
        //                }
        //                // If we didn't transform a property,
        //                // do the whole object
        //                if (!transformed) ent.TransformBy(mat);
        //            }
        //            tr.Commit();
        //        }
        //    }
        //    catch (Autodesk.AutoCAD.Runtime.Exception ex)
        //    {
        //        ed.WriteMessage("\nCould not transform entity: {0}", ex.Message);
        //    }
        //}
        #endregion

        //Get a list of all block names
        //[CommandMethod("ListBlocks")]
        //public void ListBlocks()
        //{
        //    Document doc = acadApp.DocumentManager.MdiActiveDocument;
        //    Database db = doc.Database;
        //    Editor ed = doc.Editor;

        //    Transaction tr = db.TransactionManager.StartTransaction();
        //    using (tr)
        //    {
        //        BlockTable bt = (BlockTable)tr.GetObject(db.BlockTableId, OpenMode.ForRead);
        //        foreach (ObjectId id in bt)
        //        {
        //            string blockName = null;
        //            BlockTableRecord btr = (BlockTableRecord)tr.GetObject(id, OpenMode.ForRead);
        //            if (btr != null)
        //            {
        //                try
        //                {
        //                    if (btr.IsFromExternalReference || btr.IsAnonymous)
        //                    {
        //                        //disregard
        //                    }
        //                    else if (btr.IsDynamicBlock)
        //                    {
        //                        blockName = btr.Name;
        //                        ed.WriteMessage("\nDynamicBlock:\t{0}", blockName);
        //                    }
        //                    else
        //                    {
        //                        blockName = btr.Name;
        //                        ed.WriteMessage("\nBlock:\t\t{0}", blockName);
        //                    }
        //                }
        //                catch (Autodesk.AutoCAD.Runtime.Exception ex)
        //                {
        //                    ed.WriteMessage("\nName: {0}\tException: {1} ", blockName, ex);
        //                }
        //            }
        //        }
        //    }
        //}

        #region SortPointCollection

        //private double DeNormalize(double a, double max)
        //{
        //    // Random numbers are from 0 to 1.0, so we
        //    // multiply it out to be within our range
        //    // (this will create a value from -max to max)

        //    return (2 * a - 1) * max;
        //}

        //public Point2d Random2dPoint(Random rnd, double max)
        //{
        //    double x = rnd.NextDouble(),
        //           y = rnd.NextDouble();

        //    return new Point2d(DeNormalize(x, max), DeNormalize(y, max));
        //}

        //public Point3d Random3dPoint(Random rnd, double max)
        //{
        //    double x = rnd.NextDouble(),
        //           y = rnd.NextDouble(),
        //           z = rnd.NextDouble();

        //    return new Point3d(DeNormalize(x, max), DeNormalize(y, max), DeNormalize(z, max));
        //}

        //public Point2dCollection Random2dPoints(int num, double max)
        //{
        //    Point2dCollection pts = new Point2dCollection(num);
        //    Random rnd = new Random();
        //    for (int i = 0; i < num; i++)
        //    {
        //        pts.Add(Random2dPoint(rnd, max));
        //    }
        //    return pts;
        //}

        //public Point3dCollection Random3dPoints(int num, double max)
        //{
        //    Point3dCollection pts = new Point3dCollection();
        //    Random rnd = new Random();
        //    for (int i = 0; i < num; i++)
        //    {
        //        pts.Add(Random3dPoint(rnd, max));
        //    }
        //    return pts;
        //}

        //public void PrintPoints(Editor ed, Point2dCollection pts)
        //{
        //    foreach (Point2d pt in pts)
        //    {
        //        ed.WriteMessage("{0}\n", pt);
        //    }
        //}

        //public void PrintPoints(Editor ed, Point3dCollection pts)
        //{
        //    foreach (Point3d pt in pts)
        //    {
        //        ed.WriteMessage("{0}\n", pt);
        //    }
        //}

        //[CommandMethod("PTS")]
        //public void PointSort()
        //{
        //    Document doc = acadApp.DocumentManager.MdiActiveDocument;
        //    Editor ed = doc.Editor;

        //    Point2dCollection pts2d = Random2dPoints(20, 1000.0);
        //    Point2d[] raw = pts2d.ToArray();
        //    Array.Sort(raw, new sort2dByX());
        //    Point2dCollection sorted2d = new Point2dCollection(raw);

        //    ed.WriteMessage("\n2D points before sort:\n\n");
        //    PrintPoints(ed, pts2d);
        //    ed.WriteMessage("\n\n2D points after sort:\n\n");
        //    PrintPoints(ed, sorted2d);

        //    Point3dCollection pts3d = Random3dPoints(20, 1000.0);
        //    Point3d[] raw3d = new Point3d[pts3d.Count];
        //    pts3d.CopyTo(raw3d, 0);
        //    Array.Sort(raw3d, new sort3dByX());
        //    Point3dCollection sorted3d = new Point3dCollection(raw3d);

        //    ed.WriteMessage("\n3D points before sort:\n\n");
        //    PrintPoints(ed, pts3d);
        //    ed.WriteMessage("\n\n3D points after sort:\n\n");
        //    PrintPoints(ed, sorted3d);
        //}

        #endregion

        #region pointMonitor
        //[CommandMethod("pointMonitor")]
        //public void createPointMonitor()
        //{
        //    //get the editor object
        //    Editor ed = Autodesk.AutoCAD.ApplicationServices.Application.DocumentManager.MdiActiveDocument.Editor;
        //    // add the delegate to the events list
        //    ed.PointMonitor += new PointMonitorEventHandler(ContextMonitor);
        //}

        //[CommandMethod("endPointMonitor")]
        //public void removePointMonitor()
        //{
        //    //get the editor object
        //    Editor ed = Autodesk.AutoCAD.ApplicationServices.Application.DocumentManager.MdiActiveDocument.Editor;
        //    // remove the delegate from the events list
        //    try
        //    {
        //        ed.PointMonitor -= new PointMonitorEventHandler(ContextMonitor);
        //    }
        //    catch
        //    {

        //    }
        //}

        //private void ContextMonitor(object sender, PointMonitorEventArgs e)
        //{
        //    // check to see what is under aperture
        //    Editor ed = Autodesk.AutoCAD.ApplicationServices.Application.DocumentManager.MdiActiveDocument.Editor;
        //    //FullSubentityPath[] fullEntPath = e.Context.GetPickedEntities();

        //    //check to see if there is anything in the array
        //    //if (fullEntPath.Length > 0)
        //    //{
        //        // we can display temp graphics
        //        // start the transaction
        //        Transaction trans = Autodesk.AutoCAD.ApplicationServices.Application.DocumentManager.MdiActiveDocument.Database.TransactionManager.StartTransaction();
        //        try
        //        {                   
        //            //ObjectId[] ids = fullEntPath[0].GetObjectIds();
        //            //ed.WriteMessage(ids[0].Database.GetType().ToString());
        //            Polyline connectionMarker = new Polyline();                   
        //            Point3dCollection pts = new Point3dCollection();
        //            DoubleCollection bulges = new DoubleCollection();
        //            //finds number pixles in square unit
        //            Point3d crosshairLocation = e.Context.RawPoint;
        //            Point2d pixels = e.Context.DrawContext.Viewport.GetNumPixelsInUnitSquare(crosshairLocation);
        //            // setup the constant radius relative to current zoom
        //            double connectionMarkerRad = 20;// / pixels.X;
        //            double connectionMarkerThick = connectionMarkerRad / 8;

        //            connectionMarker.ColorIndex = 3;

        //            pts.Add(e.Context.RawPoint);
        //            pts.Add(e.Context.RawPoint.Add(new Vector3d(Math.Cos(0 * Math.PI / 180) * connectionMarkerRad, Math.Sin(0 * Math.PI / 180) * connectionMarkerRad, 0)));
        //            pts.Add(e.Context.RawPoint.Add(new Vector3d(Math.Cos(360 / 2 * Math.PI / 180) * connectionMarkerRad, Math.Sin(360 / 2 * Math.PI / 180) * connectionMarkerRad, 0)));
        //            pts.Add(e.Context.RawPoint.Add(new Vector3d(Math.Cos(360 * Math.PI / 180) * connectionMarkerRad, Math.Sin(360 * Math.PI / 180) * connectionMarkerRad, 0)));
        //            bulges.Add(0);
        //            bulges.Add(Math.Tan((360 / 8) * Math.PI / 180));
        //            bulges.Add(Math.Tan((360 / 8) * Math.PI / 180));
        //            bulges.Add(0);
        //            for (short i = 0; i <= pts.Count - 1; i++)
        //            {
        //                connectionMarker.AddVertexAt(i, pts[i].Convert2d(new Plane(new Point3d(0, 0, 0), new Vector3d(0, 0, 1))), bulges[i], 0, 0);
        //            }
        //            connectionMarker.RemoveVertexAt(0);
        //            connectionMarker.ConstantWidth = connectionMarkerThick;
        //            //if fullEntPath.ToString().Contains("Block") 
        //            e.Context.DrawContext.Geometry.Polyline(connectionMarker, 0, connectionMarker.NumberOfVertices-1);
        //            System.Threading.Thread.Sleep(50);
        //            //e.Context.DrawContext.Geometry.Circle(e.Context.RawPoint, 2, Vector3d.ZAxis);
        //            trans.Commit();
        //        }
        //        catch (Autodesk.AutoCAD.Runtime.Exception ex)
        //        {
        //            ed = Autodesk.AutoCAD.ApplicationServices.Application.DocumentManager.MdiActiveDocument.Editor;
        //            ed.WriteMessage("Error: {0}", ex.ToString());
        //        }
        //        trans.Dispose();
        //    //}
        //    return;
        //}
        #endregion 

        #region PipeHoop
        //Polyline arcPolySegment = new Polyline();
        //[CommandMethod("PH")]
        //public void PipeHoop()
        //{
        //    Document doc = acadApp.DocumentManager.MdiActiveDocument;
        //    Editor ed = doc.Editor;
        //    Database db = doc.Database;
        //    BlockTable bt;
        //    BlockTableRecord btr;
        //    Polyline newPoly = new Polyline();
        //    Transaction tr = db.TransactionManager.StartTransaction();

        //    TypedValue[] filList = new TypedValue[] { new TypedValue((int)DxfCode.Start, "INSERT") };
        //    SelectionFilter filter = new SelectionFilter(filList);
        //    //bool removeVertex = false;
        //    Point3d vertToRemove = new Point3d();

        //    using (tr)
        //    {
        //        bt = (BlockTable)tr.GetObject(db.BlockTableId, OpenMode.ForRead);
        //        btr = (BlockTableRecord)tr.GetObject(bt[BlockTableRecord.ModelSpace], OpenMode.ForRead);
        //        ObjectId curveIdin = new ObjectId();
        //        ObjectIdCollection objIds = new ObjectIdCollection();
        //        Curve curveIn = null;

        //        PromptEntityOptions peo = new PromptEntityOptions("\nSelect Pipe to Hoop:");
        //        peo.SetRejectMessage("\nRejected Class, Choose something else.");
        //        //peo.AddAllowedClass(typeof(Spline), false);
        //        peo.AddAllowedClass(typeof(Polyline), false);
        //        //peo.AddAllowedClass(typeof(Line), false);
        //        peo.AllowObjectOnLockedLayer = true;
        //        peo.AllowNone = false;
        //        PromptEntityResult per = ed.GetEntity(peo);
        //        if (per.Status == PromptStatus.OK)
        //        {
        //            curveIdin = per.ObjectId;
        //            curveIn = (Curve)tr.GetObject(curveIdin, OpenMode.ForRead) as Curve;
        //        }
        //        peo.Message = "\nSelect Pipes and/or Blocks to Hoop Over. Press Enter when done.:";

        //        bool perOk = true;
        //        while (perOk && curveIdin != null)
        //        {
        //            per = ed.GetEntity(peo);

        //            if (per.Status == PromptStatus.OK)
        //            {

        //                objIds.Add(per.ObjectId);
        //            }
        //            else perOk = false;
        //        }

        //        HSKDICommon.Commands.removeDuplicates(ref objIds);

        //        Point3dCollection intersectCollection = new Point3dCollection();

        //        foreach (ObjectId id in objIds)
        //        {
        //            Point3dCollection intersects = new Point3dCollection();

        //            Polyline plIn = new Polyline();

        //            plIn = (Polyline)tr.GetObject(curveIdin, OpenMode.ForRead);
        //            //Spline splin = (Spline)tr.GetObject(hoopcurveId, OpenMode.ForRead);
        //            //Line lin = (Line)tr.GetObject(hoopcurveId, OpenMode.ForRead);
        //            //BlockReference brin = (BlockReference)tr.GetObject(id, OpenMode.ForRead);                    
        //            Entity entover = (Entity)tr.GetObject(id, OpenMode.ForRead);

        //            if (plIn != null)
        //            {
        //                plIn.IntersectWith(entover, Intersect.OnBothOperands, intersects, IntPtr.Zero, IntPtr.Zero);
        //                if (intersects.Count != 0)
        //                {
        //                    if (intersects.Count == 1)
        //                    {
        //                        double radius = .05 * HSKDICommon.Commands.getdimscale();
        //                        Point3d c = plIn.GetClosestPointTo(intersects[0], true);
        //                        //Point3d p0, p1;
        //                        Circle tempCir = new Circle(c, Vector3d.ZAxis, radius);
        //                        foreach (Point3d i in intersectCollection)
        //                        {
        //                            if (c.DistanceTo(i) <= radius)
        //                            {
        //                                //removeVertex = true;
        //                                vertToRemove = i;
        //                            }
        //                        }
        //                        intersectCollection.Add(c);

        //                        newPoly = (Polyline)plIn.Clone();
        //                        newPoly.Color = Autodesk.AutoCAD.Colors.Color.FromColorIndex(ColorMethod.ByAci, 4);

        //                        Point3dCollection pts = new Point3dCollection();
        //                        tempCir.IntersectWith(plIn, Intersect.OnBothOperands, pts, IntPtr.Zero, IntPtr.Zero);

        //                        if (pts.Count == 2)
        //                        {
        //                            int pt1OnSegment = -1, pt2OnSegment = -1;

        //                            for (int i = 0; i < plIn.NumberOfVertices - 1; i++)
        //                            {
        //                                if (plIn.OnSegmentAt(i, new Point2d(pts[0].X, pts[0].Y), 0)) pt1OnSegment = i;
        //                                if (plIn.OnSegmentAt(i, new Point2d(pts[1].X, pts[1].Y), 0)) pt2OnSegment = i;
        //                            }

        //                            if (pt1OnSegment == -1 || pt2OnSegment == -1)
        //                            {
        //                            }
        //                            else
        //                            {
        //                                double width = (plIn.GetStartWidthAt(pt1OnSegment));
        //                                switch (Math.Sign(pt1OnSegment - pt2OnSegment))
        //                                {
        //                                    case -1:
        //                                        // intersections on more than one segment
        //                                        // lower bound is on lower numbered segment
        //                                        // no reversals needed

        //                                        if (newPoly.GetSegmentType(pt1OnSegment) == SegmentType.Line)
        //                                        {
        //                                            newPoly.AddVertexAt(pt1OnSegment + 1, new Point2d(pts[0].X, pts[0].Y), Math.Tan((Math.PI) / 4), width, width);
        //                                        }
        //                                        if (newPoly.GetSegmentType(pt2OnSegment) == SegmentType.Line)
        //                                        {
        //                                            newPoly.AddVertexAt(pt2OnSegment + 1, new Point2d(pts[1].X, pts[1].Y), 0, width, width);
        //                                        }
        //                                        newPoly.RemoveVertexAt(pt1OnSegment + 2);
        //                                        break;
        //                                    case 0:
        //                                        // intersections on the same segment
        //                                        // need to find which one is closer to start.
        //                                        // possible reversal needed.
        //                                        if (newPoly.GetPoint3dAt(pt1OnSegment).DistanceTo(pts[0]) > newPoly.GetPoint3dAt(pt2OnSegment).DistanceTo(pts[1]))
        //                                        {
        //                                            Point3d tempPt = pts[0];
        //                                            pts[0] = pts[1];
        //                                            pts[1] = tempPt;
        //                                        }

        //                                        if (newPoly.GetSegmentType(pt1OnSegment) == SegmentType.Line)
        //                                        {
        //                                            newPoly.AddVertexAt(pt1OnSegment + 1, new Point2d(pts[0].X, pts[0].Y), Math.Tan((Math.PI) / 4), width, width);
        //                                            newPoly.AddVertexAt(pt1OnSegment + 2, new Point2d(pts[1].X, pts[1].Y), 0, width, width);
        //                                        }
        //                                        //else if (plIn.GetSegmentType(pt1OnSegment) == SegmentType.Arc)
        //                                        //{

        //                                        //}
        //                                        break;
        //                                    case 1:
        //                                        // intersections on more than one segment
        //                                        // lower bound is on upper numbered segment
        //                                        // curve needs to be reversed.
        //                                        newPoly.ReverseCurve();
        //                                        for (int i = 0; i < newPoly.NumberOfVertices - 1; i++)
        //                                        {
        //                                            if (newPoly.OnSegmentAt(i, new Point2d(pts[0].X, pts[0].Y), 0)) pt1OnSegment = i;
        //                                            if (newPoly.OnSegmentAt(i, new Point2d(pts[1].X, pts[1].Y), 0)) pt2OnSegment = i;
        //                                        }

        //                                        if (newPoly.GetSegmentType(pt1OnSegment) == SegmentType.Line)
        //                                        {
        //                                            newPoly.AddVertexAt(pt1OnSegment + 1, new Point2d(pts[0].X, pts[0].Y), Math.Tan((Math.PI) / 4), width, width);
        //                                        }
        //                                        if (newPoly.GetSegmentType(pt2OnSegment) == SegmentType.Line)
        //                                        {
        //                                            newPoly.AddVertexAt(pt2OnSegment + 1, new Point2d(pts[1].X, pts[1].Y), 0, width, width);
        //                                        }
        //                                        newPoly.RemoveVertexAt(pt1OnSegment + 2);
        //                                        break;
        //                                }

        //                                //int j = 0, k = 0;

        //                                //if (plIn.GetSegmentType(i) == SegmentType.Line)
        //                                //{
        //                                //    if (new Line3d(plIn.GetPoint3dAt(i), plIn.GetPoint3dAt(i + 1)).IsColinearTo(new Line3d(pts[0], pts[1])))
        //                                //    {
        //                                //        HoopAddtoPL(ref newPoly, plIn, intersects, pts, i, ref j, ref k);
        //                                //    }
        //                                //    else
        //                                //    {
        //                                //        // not on this line ??
        //                                //    }
        //                                //}
        //                                //else if (plIn.GetSegmentType(i) == SegmentType.Arc)
        //                                //{
        //                                //    if (plIn.GetArcSegmentAt(i).GetClosestPointTo(pts[0]).Point == pts[0] && plIn.GetArcSegmentAt(i).GetClosestPointTo(pts[1]).Point == pts[1])
        //                                //    {
        //                                //        // we are on the curve.
        //                                //        HoopAddtoPL(ref newPoly, plIn, intersects, pts, i, ref j, ref k);
        //                                //    }
        //                                //}
        //                                btr.UpgradeOpen();
        //                                btr.AppendEntity(newPoly);
        //                                tr.AddNewlyCreatedDBObject(newPoly, true);
        //                                btr.DowngradeOpen();
        //                                bool askToFlip = true;
        //                                while (askToFlip)
        //                                {
        //                                    PromptKeywordOptions pko = new PromptKeywordOptions("\nFlip/Accept? <Flip> [Flip/Accept]");
        //                                    pko.Keywords.Clear();
        //                                    pko.Keywords.Add("Flip");
        //                                    pko.Keywords.Add("Accept");
        //                                    pko.Keywords.Default = "Flip";
        //                                    pko.AllowArbitraryInput = false;
        //                                    pko.AllowNone = true;
        //                                    PromptResult pkr = ed.GetKeywords(pko);
        //                                    if (pkr.Status == PromptStatus.OK ||
        //                                        pkr.Status == PromptStatus.None ||
        //                                        pkr.Status == PromptStatus.Cancel ||
        //                                        pkr.Status == PromptStatus.Error ||
        //                                        pkr.StringResult == "Accept")
        //                                    {
        //                                        askToFlip = false;
        //                                        break;
        //                                    }
        //                                    else if (pkr.StringResult == "Flip") newPoly.GetLineSegmentAt(pt1OnSegment + 1).Direction.Negate();
        //                                    btr.UpgradeOpen();
        //                                    btr.AppendEntity(newPoly);
        //                                    tr.AddNewlyCreatedDBObject(newPoly, true);
        //                                    btr.DowngradeOpen();
        //                                }
        //                            }
        //                        }
        //                        else
        //                        {
        //                            ed.WriteMessage("Too many or few intersections");
        //                        }
        //                    }
        //                }
        //                curveIdin = newPoly.ObjectId;

        //            }
        //            if (newPoly.NumberOfVertices != plIn.NumberOfVertices)
        //            {
        //                plIn.UpgradeOpen();
        //                plIn.Erase();
        //                plIn.DowngradeOpen();
        //            }

        //            else intersects.Clear();
        //        }
        //        tr.Commit();
        //    }
        //}

        //private static void HoopAddtoPL(ref Polyline newPoly, Polyline plIn, Point3dCollection intersects, Point3dCollection pts, int i, ref int j, ref int k)
        //{
        //    // intersection falls completely in this segment
        //    if (plIn.GetPoint3dAt(i).DistanceTo(pts[0]) < plIn.GetPoint3dAt(i).DistanceTo(pts[1]))
        //    {
        //        // arc from pts[0] -> pts[1]
        //        j = 0;
        //        k = 1;
        //    }
        //    else if (plIn.GetPoint3dAt(i).DistanceTo(pts[0]) > plIn.GetPoint3dAt(i).DistanceTo(pts[1]))
        //    {
        //        // arc from pts[1] -> pts[0]
        //        j = 1;
        //        k = 0;
        //    }
        //    else
        //    {
        //        //some unaccounted for error
        //    }
        //    if (j != k)
        //    {
        //        double b1 = 0, b2 = 0, b3 = 0;

        //        if (newPoly.GetSegmentType(i) == SegmentType.Arc)
        //        {
        //            Point3d bcenter = newPoly.GetArcSegmentAt(i).Center;
        //            Vector3d v0 = bcenter.GetVectorTo(newPoly.GetLineSegmentAt(i).StartPoint);
        //            Vector3d v1 = bcenter.GetVectorTo(pts[j]);
        //            Vector3d v2 = bcenter.GetVectorTo(pts[k]);
        //            Vector3d v3 = bcenter.GetVectorTo(newPoly.GetLineSegmentAt(i).EndPoint);
        //            b1 = Math.Tan(v0.GetAngleTo(v1) / 4);
        //            b2 = Math.Tan((Math.PI + v1.GetAngleTo(v2)) / 4);
        //            b3 = Math.Tan(v2.GetAngleTo(v3) / 4);
        //        }
        //        else if (newPoly.GetSegmentType(i) == SegmentType.Line)
        //        {
        //            b2 = Math.Tan((Math.PI) / 4);
        //        }

        //        newPoly.SetBulgeAt(i, b1);
        //        newPoly.AddVertexAt(
        //            i + 1,
        //            new Point2d(pts[j].X, pts[j].Y),
        //            b2,
        //            plIn.GetStartWidthAt(i),
        //            (plIn.GetStartWidthAt(i) + plIn.GetEndWidthAt(i)) / 2);
        //        newPoly.AddVertexAt(
        //            i + 2,
        //            new Point2d(pts[k].X, pts[k].Y),
        //            b3,
        //            plIn.GetStartWidthAt(i),
        //            (plIn.GetStartWidthAt(i) + plIn.GetEndWidthAt(i + 1)) / 2);
        //    }
        //}

        //private static double BulgeFromCurve(Curve cv, bool clockwise)
        //{
        //    double bulge = 0.0;
        //    Arc a = cv as Arc;
        //    if (a != null)
        //    {
        //        double newStart;
        //        // The start angle is usually greater than the end,
        //        // as arcs are all counter-clockwise.
        //        // (If it isn't it's because the arc crosses the
        //        // 0-degree line, and we can subtract 2PI from the
        //        // start angle.)
        //        if (a.StartAngle > a.EndAngle) newStart = a.StartAngle - 8 * Math.Atan(1);
        //        else newStart = a.StartAngle;

        //        // Bulge is defined as the tan of
        //        // one fourth of the included angle
        //        bulge = Math.Tan((a.EndAngle - newStart) / 4);

        //        // If the curve is clockwise, we negate the bulge
        //        if (clockwise) bulge = -bulge;
        //    }
        //    return bulge;
        //}

        //private void PipeHoop_MouseClicked(object sender, MouseEventArgs e)
        //{
        //    if (e.Clicks == 1)
        //    {
        //        arcPolySegment.TransformBy(Matrix3d.Mirroring(new Line3d(arcPolySegment.StartPoint, arcPolySegment.EndPoint)));
        //    }
        //}

        #endregion
    }

    //#region HSKDICustomSnap
    //public class HSKDICustomSnap
    //{
    //    const string regAppName = "TTIF_SNAP";
    //    private static OSOverrule _osOverrule = null;
    //    private static IntOverrule _geoOverrule = null;

    //    // Object Snap Overrule to prevent snapping to objects
    //    // with certain XData attached

    //    public class OSOverrule : OsnapOverrule
    //    {
    //        public OSOverrule()
    //        {
    //            // Tell AutoCAD to filter on our application name
    //            // (this should mean our overrule only gets called
    //            // on objects possessing XData with this name)
    //            SetXDataFilter(regAppName);
    //        }

    //        public override void GetObjectSnapPoints(Entity ent, ObjectSnapModes mode, IntPtr gsm, Point3d pick, Point3d last, Matrix3d view, Point3dCollection snap, IntegerCollection geomIds)
    //        {
    //        }

    //        public override void GetObjectSnapPoints(Entity ent, ObjectSnapModes mode, IntPtr gsm, Point3d pick, Point3d last, Matrix3d view, Point3dCollection snaps, IntegerCollection geomIds, Matrix3d insertion)
    //        {
    //        }

    //        public override bool IsContentSnappable(Entity entity)
    //        {
    //            return false;
    //        }
    //    }

    //    // Geometry Overrule to prevent IntersectsWith() working on
    //    // objects with certain XData attached

    //    public class IntOverrule : GeometryOverrule
    //    {
    //        public IntOverrule()
    //        {
    //            // Tell AutoCAD to filter on our application name
    //            // (this should mean our overrule only gets called
    //            // on objects possessing XData with this name)

    //            SetXDataFilter(regAppName);
    //        }
    //        public override void IntersectWith(Entity ent1, Entity ent2, Intersect intType, Plane proj, Point3dCollection points, IntPtr thisGsm, IntPtr otherGsm)
    //        {
    //        }

    //        public override void IntersectWith(Entity ent1, Entity ent2, Intersect intType, Point3dCollection points, IntPtr thisGsm, IntPtr otherGsm)
    //        {
    //        }
    //    }

    //    private static void ToggleOverruling(bool on)
    //    {
    //        if (on)
    //        {
    //            if (_osOverrule == null)
    //            {
    //                _osOverrule = new OSOverrule();

    //                ObjectOverrule.AddOverrule(RXObject.GetClass(typeof(Entity)), _osOverrule, false);
    //            }

    //            if (_geoOverrule == null)
    //            {
    //                _geoOverrule = new IntOverrule();

    //                ObjectOverrule.AddOverrule(RXObject.GetClass(typeof(Entity)), _geoOverrule, false);
    //            }

    //            ObjectOverrule.Overruling = true;
    //        }
    //        else
    //        {
    //            if (_osOverrule != null)
    //            {
    //                ObjectOverrule.RemoveOverrule(RXObject.GetClass(typeof(Entity)), _osOverrule);

    //                _osOverrule.Dispose();
    //                _osOverrule = null;
    //            }

    //            if (_geoOverrule != null)
    //            {
    //                ObjectOverrule.RemoveOverrule(RXObject.GetClass(typeof(Entity)), _geoOverrule);

    //                _geoOverrule.Dispose();
    //                _geoOverrule = null;
    //            }

    //            // I don't like doing this and so have commented it out:
    //            // there's too much risk of stomping on other overrules...

    //            // ObjectOverrule.Overruling = false;
    //        }
    //    }

    //    [CommandMethod("DISNAP")]
    //    public static void DisableSnapping()
    //    {
    //        var doc = Autodesk.AutoCAD.ApplicationServices.Application.DocumentManager.MdiActiveDocument;
    //        var db = doc.Database;
    //        var ed = doc.Editor;

    //        // Start by getting the entities to disable snapping for.
    //        // If none selected, turn off the overrule

    //        var psr = ed.GetSelection();

    //        if (psr.Status != PromptStatus.OK)
    //            return;

    //        ToggleOverruling(true);

    //        // Start a transaction to modify the entities' XData

    //        using (var tr = doc.TransactionManager.StartTransaction())
    //        {
    //            // Make sure our RegAppID is in the table

    //            var rat =
    //              (RegAppTable)tr.GetObject(db.RegAppTableId, OpenMode.ForRead);

    //            if (!rat.Has(regAppName))
    //            {
    //                rat.UpgradeOpen();
    //                var ratr = new RegAppTableRecord();
    //                ratr.Name = regAppName;
    //                rat.Add(ratr);
    //                tr.AddNewlyCreatedDBObject(ratr, true);
    //            }

    //            // Create the XData and set it on the object

    //            using (var rb = new ResultBuffer(new TypedValue((int)DxfCode.ExtendedDataRegAppName, regAppName), new TypedValue((int)DxfCode.ExtendedDataInteger16, 1)))
    //            {
    //                foreach (SelectedObject so in psr.Value)
    //                {
    //                    var ent =
    //                      tr.GetObject(so.ObjectId, OpenMode.ForWrite) as Entity;
    //                    if (ent != null)
    //                    {
    //                        ent.XData = rb;
    //                    }
    //                }
    //            };

    //            tr.Commit();
    //        }
    //    }

    //    [CommandMethod("ENSNAP")]
    //    public static void EnableSnapping()
    //    {
    //        var doc = Autodesk.AutoCAD.ApplicationServices.Application.DocumentManager.MdiActiveDocument;
    //        var db = doc.Database;
    //        var ed = doc.Editor;

    //        // Start by getting the entities to enable snapping for

    //        var pso = new PromptSelectionOptions();
    //        pso.MessageForAdding = "Select objects (none to remove overrule)";
    //        var psr = ed.GetSelection(pso);

    //        if (psr.Status == PromptStatus.Error)
    //        {
    //            ToggleOverruling(false);
    //            ed.WriteMessage("\nOverruling turned off.");
    //            return;
    //        }
    //        else if (psr.Status != PromptStatus.OK)
    //            return;

    //        // Start a transaction to modify the entities' XData

    //        using (var tr = doc.TransactionManager.StartTransaction())
    //        {
    //            // Create a ResultBuffer and use it to remove the XData
    //            // from the object

    //            using (var rb = new ResultBuffer(new TypedValue((int)DxfCode.ExtendedDataRegAppName, regAppName)))
    //            {
    //                foreach (SelectedObject so in psr.Value)
    //                {
    //                    var ent =
    //                      tr.GetObject(so.ObjectId, OpenMode.ForWrite) as Entity;
    //                    if (ent != null)
    //                    {
    //                        ent.XData = rb;
    //                    }
    //                }
    //            };

    //            tr.Commit();
    //        }
    //    }
    //}

    //#endregion


    //#region SortPointCollectionHelper

    //internal class sort2dByX : IComparer<Point2d>
    //{
    //    public static bool IsZero(double a)
    //    {
    //        return Math.Abs(a) < Tolerance.Global.EqualPoint;
    //    }

    //    public static bool IsEqual(double a, double b)
    //    {
    //        return IsZero(b - a);
    //    }

    //    public int Compare(Point2d a, Point2d b)
    //    {
    //        if (IsEqual(a.X, b.X)) return 0; // ==
    //        if (a.X < b.X) return -1; // <
    //        return 1; // >
    //    }
    //}

    //internal class sort3dByX : IComparer<Point3d>
    //{
    //    public static bool IsZero(double a)
    //    {
    //        return Math.Abs(a) < Tolerance.Global.EqualPoint;
    //    }

    //    public static bool IsEqual(double a, double b)
    //    {
    //        return IsZero(b - a);
    //    }

    //    public int Compare(Point3d p, Point3d q)
    //    {
    //        int diff = Compare(p.X, q.X);
    //        if (0 == diff)
    //        {
    //            diff = Compare(p.Y, q.Y);
    //            if (0 == diff)
    //            {
    //                diff = Compare(p.Z, q.Z);
    //            }
    //        }
    //        return diff;
    //    }

    //    public int Compare(double a, double b)
    //    {
    //        return IsEqual(a, b) ? 0 : (a < b ? -1 : 1);
    //    }
    //}

    //#endregion

    //#region MemoryHog
    //public class MemoryConsumingApp
    //{
    //    // Helper function to check for adequate memory

    //    bool CanGetMemory(int megabytes)
    //    {
    //        try
    //        {
    //            MemoryFailPoint mfp = new MemoryFailPoint(megabytes);
    //        }
    //        catch (InsufficientMemoryException)
    //        {
    //            return false;
    //        }
    //        return true;
    //    }

    //    [CommandMethod("MEM")]
    //    public void CheckForMemoryBeforeRunning()
    //    {
    //        Document doc = acadApp.DocumentManager.MdiActiveDocument;
    //        Editor ed = doc.Editor;

    //        // Ask for the amount of memory for which to check

    //        PromptIntegerOptions pio = new PromptIntegerOptions("\nEnter amount of memory (in megabytes) to check for: ");
    //        pio.AllowNegative = false;
    //        pio.AllowNone = false;
    //        pio.AllowZero = false;

    //        PromptIntegerResult pir = ed.GetInteger(pio);

    //        // Check for the memory

    //        bool canProceed = CanGetMemory(pir.Value);

    //        ed.WriteMessage("\n{0}ufficient memory to complete operation.", canProceed ? "S" : "Ins");

    //        if (!canProceed) return;

    //        // Perform operation
    //        // ...
    //    }
    //}
    //#endregion

    //#region UCSSPY
    ///// <summary>
    ///// General utility functions called throughout the application.
    ///// </summary>    
    //internal class Utils
    //{
    //    /// <summary>
    //    /// Gets a value indicating whether a script is active.
    //    /// </summary>
    //    /// <returns>
    //    /// True if a script is active, false otherwise.
    //    /// </returns> 
    //    internal static bool ScriptActive
    //    {
    //        get
    //        {
    //            return (((short)acadApp.GetSystemVariable("CMDACTIVE") & 4) == 4);
    //        }
    //    }

    //    /// <summary>
    //    /// Gets a value indicating whether the current UCS is
    //    /// equivalent to the WCS.
    //    /// </summary> 
    //    internal static bool WcsCurrent
    //    {
    //        get
    //        {
    //            return (short)acadApp.GetSystemVariable("WORLDUCS") == 1;
    //        }
    //    }

    //    /// <summary>
    //    /// Gets or sets a value indicating whether UCSFOLLOW is on or
    //    /// off.
    //    /// </summary>        
    //    internal static bool UcsFollow
    //    {
    //        get
    //        {
    //            return (short)acadApp.GetSystemVariable("UCSFOLLOW") == 1;
    //        }
    //        set
    //        {
    //            acadApp.SetSystemVariable("UCSFOLLOW", value ? 1 : 0);
    //        }
    //    }
    //}

    ///// <summary>
    ///// Per-document data and event registration.
    ///// </summary> 
    //internal class DocData
    //{
    //    /// <summary>
    //    /// Gets or sets a value indicating the application ID
    //    /// </summary>        
    //    internal static readonly Guid AppId;

    //    /// <summary>
    //    /// Gets the Document that data is stored for
    //    /// and events are registered upon.
    //    /// </summary> 
    //    internal readonly Document Document;

    //    /// <summary>
    //    /// List of command names that we monitor.
    //    /// </summary>        
    //    private static readonly List<string> commandNames;

    //    /// <summary>
    //    /// Initializes static members of the DocData class.
    //    /// </summary>        
    //    static DocData()
    //    {
    //        string cmdsToWatch = "XATTACH,IMAGEATTACH,DWFATTACH";
    //        if (acadApp.Version.Major == 17 & acadApp.Version.Minor >= 1) cmdsToWatch += ",DGNATTACH";
    //        else if ((acadApp.Version.Major == 18 & acadApp.Version.Minor >= 1) | acadApp.Version.Major > 18) cmdsToWatch += ",DGNATTACH,PDFATTACH,POINTCLOUDATTACH";

    //        DocData.commandNames = new List<string>(cmdsToWatch.Split(','));
    //        DocData.AppId = new Guid();

    //        acadApp.DocumentManager.DocumentCreated += (sender, e) =>
    //        {
    //            e.Document.UserData.Add(DocData.AppId, new DocData(e.Document));
    //        };
    //    }

    //    /// <summary>
    //    /// Initializes a new instance of the DocData class.
    //    /// Main constructor. initialises all the event registrations.
    //    /// </summary>

    //    /// <param name="doc">Document to register events upon.</param>

    //    internal DocData(Document doc)
    //    {
    //        this.Document = doc;

    //        this.CurrentUCS = Matrix3d.Identity;

    //        this.Document.CommandWillStart += new CommandEventHandler(this.Document_CommandWillStart);
    //        this.Document.CommandCancelled += this.Doc_CommandFinished;
    //        this.Document.CommandEnded += this.Doc_CommandFinished;
    //        this.Document.CommandFailed += this.Doc_CommandFinished;
    //    }

    //    /// <summary>
    //    /// Gets or sets the current UCS matrix for
    //    /// restoration at completion of commands.
    //    /// </summary> 
    //    private Matrix3d CurrentUCS { get; set; }

    //    /// <summary>
    //    /// Gets or sets a value indicating whether
    //    /// the UCS has been changed.
    //    /// </summary>        
    //    private bool ChangedUCS { get; set; }

    //    /// <summary>
    //    /// Gets or sets a value indicating whether the
    //    /// UCSFOLLOW system variable has been changed.
    //    /// </summary>
    //    private bool ChangedUcsFollow { get; set; }

    //    /// <summary>
    //    /// Called upon first time load to register events upon
    //    /// all open documents.
    //    /// </summary>
    //    internal static void Initialise()
    //    {
    //        foreach (Document doc in acadApp.DocumentManager)
    //        {
    //            doc.UserData.Add(DocData.AppId, new DocData(doc));
    //        }
    //    }

    //    /// <summary>
    //    /// Event handler for CommandWillStart. It checks if the command
    //    /// starting is of interest and if the WCS is not current, then
    //    /// prompts the user to change the UCS.
    //    /// </summary>
    //    /// <param name="sender">Document this command started in</param>
    //    /// <param name="e">
    //    /// Arguments for the event including the name of the command
    //    /// </param>
    //    private void Document_CommandWillStart(object sender, CommandEventArgs e)
    //    {
    //        // Get the 'can we run' conditionals out of the way
    //        // first up, is this a command of interest?
    //        if (!DocData.commandNames.Contains(e.GlobalCommandName)) return;

    //        // Next, is the WCS already current?
    //        if (Utils.WcsCurrent) return;

    //        // Lastly, is a script active? If it is then we don't want
    //        // to interrupt it with a dialog box
    //        if (Utils.ScriptActive) return;

    //        // Check if UCSFOLLOW is on and turn it off
    //        if (Utils.UcsFollow)
    //        {
    //            Utils.UcsFollow = false;
    //            this.ChangedUcsFollow = true;
    //        }

    //        // To use a MessageBox implementation instead of a TaskDialog,
    //        // uncomment the following MessageBox region and comment out
    //        // the TaskDialog region as well as removing the reference to
    //        // AdWindows.dll and the "using Autodesk.Windows;" statement

    //        #region MessageBox warning implementation

    //        DialogResult ret = MessageBox.Show(
    //            "The current UCS is not equivalent to the WCS.\n" +
    //            "Do you want to change to the WCS for the duration of this command?", "RefUcsSpy",
    //            MessageBoxButtons.YesNo,
    //            MessageBoxIcon.Question);
    //        if (ret == DialogResult.Yes)
    //        {
    //            // Grab the old coordsys to restore later
    //            this.CurrentUCS = this.Document.Editor.CurrentUserCoordinateSystem;
    //            // Reset the current UCS to WCS
    //            this.Document.Editor.CurrentUserCoordinateSystem = Matrix3d.Identity;
    //            // Update our flag to say we've made a change that
    //            // needs restoring when the command finishes
    //            this.ChangedUCS = true;
    //        }
    //        #endregion

    //        #region TaskDialog implementation
    //        /* 
             
    //        // Spin up a new taskdialog instead of a messagebox
    //        TaskDialog td = new TaskDialog();
    //        td.WindowTitle = "Reference UCS Spy - UCS Active";
    //        td.MainIcon = TaskDialogIcon.Warning;
    //        td.MainInstruction = "The current UCS (User Coordinate System) does not match the WCS (World Coordinate System)."
    //            + "\nWhat do you want to do?";
    //        td.ContentText =
    //            "Attaching reference files using a coordinate system other than the WCS can lead to undesirable results";
    //        td.UseCommandLinks = true;
    //        td.AllowDialogCancellation = true;
            
    //        td.FooterIcon = TaskDialogIcon.Information;
    //        td.FooterText = "It is common to have reference files drawn to a specific coordinate system (WCS) and then inserted at (0,0,0) to allow all files that share the same coordinate system to overlay each other";
    //        td.Buttons.Add(new TaskDialogButton(0, "Change UCS to WCS for this attachment"));
    //        td.Buttons.Add(new TaskDialogButton(1, "Ignore current UCS and continue"));
            
    //        // Set default to change
    //        td.DefaultButton = 0;
            
    //        td.Callback = (ActiveTaskDialog tskDlg, TaskDialogCallbackArgs eventargs, object s) => 
    //        {
    //            if (eventargs.Notification == TaskDialogNotification.ButtonClicked)
    //            {
    //                switch (eventargs.ButtonId)
    //                {
    //                    case 0:
    //                        // Grab the old coordsys to restore later
    //                        this.CurrentUCS = this.Document.Editor.CurrentUserCoordinateSystem;
    //                        // Reset the current UCS to WCS
    //                        this.Document.Editor.CurrentUserCoordinateSystem = Matrix3d.Identity;
    //                        // Update our flag to say we've made a change that
    //                        // needs restoring when the command finishes
    //                        this.ChangedUCS = true;
    //                        break;
    //                    case 1:
    //                        // ignore it
    //                        break;
    //                    default:
    //                        break;
    //                }
    //            }
    //            return false;
    //        };
    //        td.Show(acadApp.MainWindow.Handle);
            
    //        */
    //        #endregion
    //    }

    //    /// <summary>
    //    /// Event handler for CommandEnded, CommandCancelled and
    //    /// CommandFailed.
    //    /// Resets the UCS to what was stored previously before
    //    /// reference file attachment.
    //    /// </summary>
    //    /// <param name="sender">
    //    /// The document this event is registered upon.
    //    /// </param>
    //    /// <param name="e">Arguments for this event.</param>
    //    private void Doc_CommandFinished(object sender, CommandEventArgs e)
    //    {
    //        if (this.ChangedUCS)
    //        {
    //            this.ChangedUCS = false;
    //            this.Document.Editor.CurrentUserCoordinateSystem = this.CurrentUCS;
    //            this.CurrentUCS = Matrix3d.Identity;
    //        }
    //        if (this.ChangedUcsFollow)
    //        {
    //            Utils.UcsFollow = true;
    //            this.ChangedUcsFollow = false;
    //        }
    //    }
    //}

    ///// <summary>
    ///// Main entrypoint of the application. Initialises document
    ///// creation/destruction event hooks.
    ///// </summary>
    //public class Entrypoint : IExtensionApplication
    //{
    //    #region IExtensionApplication members

    //    /// <summary>
    //    /// Startup member - called once when first loaded.
    //    /// </summary>        
    //    void IExtensionApplication.Initialize()
    //    {
    //        DocData.Initialise();
    //    }

    //    /// <summary>
    //    /// Shutdown member - called once when AutoCAD quits.
    //    /// </summary>
    //    void IExtensionApplication.Terminate()
    //    {
    //    }
    //    #endregion
    //}
    //#endregion

    //public class NewTableRow
    //{
    //    public string layer = null;
    //    public string entType = null;
    //    public double area;
    //    public double length;
    //    public List<double> coords = new List<double>();
    //    public string blkName = null;
    //    public List<string> attTags = new List<string>();
    //    public List<string> attTexts = new List<string>();
    //    public TypedValue[] xData = null;
    //    public string txt = null;

    //    public NewTableRow(string layer, string entType, double area, double length, List<double> coords, string blkName, List<string> attTags, List<string> attTexts, string txt, TypedValue[] xData)
    //    {
    //        this.entType = entType;
    //        this.area = area;
    //        this.length = length;
    //        this.coords = coords;
    //        this.layer = layer;
    //        this.blkName = blkName;
    //        this.attTags = attTags;
    //        this.attTexts = attTexts;
    //        this.txt = txt;
    //        this.xData = xData;
    //    }
    //}

    //public class PolylineProps
    //{
    //    public List<Point3d> pts = new List<Point3d>();
    //    public List<Double> blgs = new List<Double>();
    //    public List<Double> swdths = new List<Double>();
    //    public List<Double> ewdths = new List<Double>();

    //    public PolylineProps(List<Point3d> pts, List<Double> blgs, List<Double> swdths, List<Double> ewdths)
    //    {
    //        this.pts = pts;
    //        this.blgs = blgs;
    //        this.swdths = swdths;
    //        this.ewdths = ewdths;
    //    }
    //    public PolylineProps()
    //    {

    //    }
    //}

    

}