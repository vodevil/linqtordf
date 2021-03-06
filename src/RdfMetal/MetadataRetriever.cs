﻿using System;
using System.Collections.Generic;
using System.Linq;
using SemWeb.Remote;

namespace RdfMetal
{
    public class MetadataRetriever
    {
        public MetadataRetriever(Options opts)
        {
            this.opts = opts;
        }
        Options opts { get; set; }
        public IEnumerable<OntologyClass> GetClasses()
        {
            var r = GetClassUris()
                .Distinct()
                .Where(s => !string.IsNullOrEmpty(s));
            Dictionary<string, OntologyClass> x = new Dictionary<string, OntologyClass>();
            foreach (var s in r)
            {
                if (!x.Keys.Contains(s))
                {
                    x[s] = GetClass(s);
                }
            }
            var result = x.Values.ToList();
            ProcessClassRelationships(result);
            AnnotateClasses(result);
            return result;
        }

        private IEnumerable<string> GetClassUris()
        {
            var source = new SparqlHttpSource(opts.endpoint);
            var properties = new ClassQuerySink(opts.ignoreBlankNodes, opts.ontologyNamespace, new[] { "u" });
            source.RunSparqlQuery(sqGetClasses, properties);
            return properties.bindings.Map(nvc => nvc["u"]);
        }

        private OntologyClass GetClass(string classUri)
        {
            var u = new Uri(classUri);
            string className = GetNameFromUri(classUri); Console.WriteLine(className);
            var source = new SparqlHttpSource(opts.endpoint);
            var properties = new ClassQuerySink(opts.ignoreBlankNodes, null, new[] { "p", "r" });

            string sparqlQuery = string.Format(sqGetProperty, classUri);
            source.RunSparqlQuery(sparqlQuery, properties);
            IEnumerable<Tuple<string, string>> q1 = properties.bindings
                .Map(nvc => new Tuple<string, string>(nvc["p"], nvc["r"]))
                .Where(t => (!(string.IsNullOrEmpty(t.First) || string.IsNullOrEmpty(t.Second))));

            sparqlQuery = string.Format(sqGetObjectProperty, classUri);
            source.RunSparqlQuery(sparqlQuery, properties);
            IEnumerable<Tuple<string, string>> q2 = properties.bindings
                .Map(nvc => new Tuple<string, string>(nvc["p"], nvc["r"]))
                .Where(t => (!(string.IsNullOrEmpty(t.First) || string.IsNullOrEmpty(t.Second))));
            IEnumerable<OntologyProperty> ops = q2.Map(t => new OntologyProperty
                                                       {
                                                           Uri = t.First.Trim(),
                                                           IsObjectProp = true,
                                                           Name = GetNameFromUri(t.First),
                                                           Range = GetNameFromUri(t.Second),
                                                           RangeUri = t.Second
                                                       });
            IEnumerable<OntologyProperty> dps = q1.Map(t => new OntologyProperty
                                                       {
                                                           Uri = t.First.Trim(),
                                                           IsObjectProp = false,
                                                           Name = GetNameFromUri(t.First),
                                                           Range = GetNameFromUri(t.Second),
                                                           RangeUri = t.Second
                                                       });
            var d = new Dictionary<string, OntologyProperty>();
            IEnumerable<OntologyProperty> props = ops.Union(dps).Map(p => TranslateType(p));
            foreach (OntologyProperty prop in props)
            {
                d[prop.Uri] = prop;
            }
            var result = new OntologyClass
                             {
                                 Name = className,
                                 Uri = classUri,
                                 Properties = d.Values.Where(p => NamespaceMatches(p)).ToArray()
                             };

            return result;
        }

        private OntologyProperty TranslateType(OntologyProperty p)
        {
            string newtype;
            switch (p.Range)
            {
                case "integer":
                    newtype = "int";
                    break;
                case "Literal":
                    newtype = "string";
                    break;
                case "Thing":
                    newtype = "LinqToRdf.OwlInstanceSupertype";
                    break;
                default:
                    newtype = p.Range;
                    break;
            }
            return new OntologyProperty
                       {
                           IsObjectProp = p.IsObjectProp,
                           Name = p.Name,
                           Range = newtype,
                           RangeUri = p.RangeUri,
                           Uri = p.Uri
                       };
        }

        private string GetNameFromUri(string s)
        {
            var u = new Uri(s);
            if (!string.IsNullOrEmpty(u.Fragment))
            {
                return u.Fragment.Substring(1);
            }
            return u.Segments[u.Segments.Length - 1];
        }

        private bool NamespaceMatches(OntologyProperty p)
        {
            if (string.IsNullOrEmpty(opts.ontologyNamespace))
            {
                return true;
            }
            return p.Uri.StartsWith(opts.ontologyNamespace);
        }

        public static void ProcessClassRelationships(IEnumerable<OntologyClass> classes)
        {
            foreach (var ontCls in classes)
            {
                ontCls.IncomingRelationships = classes
                    .Map(c => c.OutgoingRelationships.AsEnumerable())
                    .Flatten()
                    .Where(p => p.Range == ontCls.Name)
                    .ToArray();
            }
        }
        private static void AnnotateClasses(IEnumerable<OntologyClass> classes)
        {
            foreach (var c in classes)
            {
                foreach (var p in c.Properties)
                {
                    if (classes.Any(x=>x.Uri == p.RangeUri))
                    {
                        p.IsObjectProp = true;
                    }
                    p.HostClass = c;
                }
            }
        }

        #region queries

        private static string sqGetClasses =
            @"
PREFIX owl:  <http://www.w3.org/2002/07/owl#>

SELECT DISTINCT ?u
WHERE
{
    {?u a owl:Class .}
UNION
    {
    ?u a ?x.
    ?x a owl:Class.
    }
}
";

        private static string sqGetObjectProperty =
            @"
PREFIX owl:  <http://www.w3.org/2002/07/owl#>
PREFIX rdfs:  <http://www.w3.org/2000/01/rdf-schema#>

SELECT DISTINCT ?p ?r
WHERE
{{
?p rdfs:domain <{0}>.
?p rdfs:range ?r.
}}";

        private static string sqGetProperty =
            @"
PREFIX owl:  <http://www.w3.org/2002/07/owl#>
PREFIX rdfs:  <http://www.w3.org/2000/01/rdf-schema#>

SELECT DISTINCT ?p ?r
WHERE
{{
?p a owl:DatatypeProperty .
?p rdfs:domain <{0}>.
?p rdfs:range ?r.
}}";

        #endregion
    }
}
