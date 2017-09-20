using Autodesk.AutoCAD.ApplicationServices;
using Autodesk.AutoCAD.EditorInput;
using Autodesk.AutoCAD.DatabaseServices;
using Autodesk.AutoCAD.Runtime;

namespace TraceBoundary
{
    public class Commands
    {
        static int _index = 1;

        [CommandMethod("TB")]
        public void TraceBoundary()
        {
            Document doc =
              Application.DocumentManager.MdiActiveDocument;
            Database db = doc.Database;
            Editor ed = doc.Editor;

            // Select a seed point for our boundary

            PromptPointResult ppr =
              ed.GetPoint("\nSelect internal point: ");

            if (ppr.Status != PromptStatus.OK)
                return;

            // Get the objects making up our boundary

            DBObjectCollection objs =
              ed.TraceBoundary(ppr.Value, true);

            if (objs.Count > 0)
            {
                Transaction tr =
                  doc.TransactionManager.StartTransaction();
                using (tr)
                {
                    // We'll add the objects to the model space

                    BlockTable bt =
                      (BlockTable)tr.GetObject(
                        doc.Database.BlockTableId,
                        OpenMode.ForRead
                      );

                    BlockTableRecord btr =
                      (BlockTableRecord)tr.GetObject(
                        bt[BlockTableRecord.ModelSpace],
                        OpenMode.ForWrite
                      );

                    // Add our boundary objects to the drawing and
                    // collect their ObjectIds for later use

                    ObjectIdCollection ids = new ObjectIdCollection();
                    foreach (DBObject obj in objs)
                    {
                        if (obj is Entity ent)
                        {
                            // Set our boundary objects to be of
                            // our auto-incremented colour index

                            ent.ColorIndex = _index;

                            // Set the lineweight of our object

                            ent.LineWeight = LineWeight.LineWeight050;

                            // Add each boundary object to the modelspace
                            // and add its ID to a collection

                            ids.Add(btr.AppendEntity(ent));
                            tr.AddNewlyCreatedDBObject(ent, true);
                        }
                    }

                    // Increment our colour index

                    _index++;

                    // Commit the transaction

                    tr.Commit();
                }
            }
        }
    }
}