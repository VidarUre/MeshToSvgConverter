using System;
using System.IO;
using System.Collections.Generic;

namespace SVGProject
{
    class SVGWriter
    {
        private String document;

        public SVGWriter(int canvasWidth, int canvasHeight)
        {
            document += "<?xml version=\"1.0\" encoding=\"iso-8859-1\"?> \n";
            document += "<svg version=\"1.1\" xmlns=\"http://www.w3.org/2000/svg\" ";
            document += "width=\"" + canvasWidth + "\" " + "height=\"" + canvasHeight + "\"> \n";
        }

        // Returns the SVG document
        public String GetSvgDogument()
        {
            return document;
        }

        // Writes all the layers of silhouettes into SVG
        public void WriteSilhouettesToSVG(List<Layer> layers, int height)
        {
            foreach (Layer l in layers)
            {
                foreach (SilhouetteGroup sg in l.SilhouetteGroups)
                {
                    document += "<g> \n";
                    foreach (Silhouette s in sg.Silhouettes)
                    {
                        foreach (SilhouettePiece sPiece in s.Pieces)
                        {
                            if (sPiece.Points.Count > 20)
                            {
                                document += "<polyline transform=\"scale(1, -1) translate(0, -" + height + ")\" points = \""; // Inverting
                                LinkedListNode<ContourPixel> currentNode = sPiece.Points.First;
                                while (currentNode != null)
                                {

                                    document += currentNode.Value.XCoord + "," + currentNode.Value.YCoord + " ";

                                    currentNode = currentNode.Next;
                                }
                                document += "\" style=\"fill:rgb(" + s.Color.r * 255 + ", " + s.Color.g * 255 + ", " + s.Color.b * 255 + ");stroke:" + "rgb(" + s.Color.r * 255 + ", " + s.Color.g * 255 + ", " + s.Color.b * 255 + ");" + "\" /> \n";
                            }
                        }
                    }
                    document += "</g> \n";
                }
            }
        }

        // Saves the SVG document with a given filename
        public void SaveDocument(String filename)
        {
            document += "</svg> \n";
            File.WriteAllText(filename, document);
        }
    }
}
