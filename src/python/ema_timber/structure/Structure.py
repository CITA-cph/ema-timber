import networkx as nx
import json
from enum import Enum

class NodeType(Enum):
    Element = 0
    Joint = 1


class Structure():
    def __init__(self, graph=None, name="Structure", attr={}):
        if graph is None:
            self.graph = nx.Graph(name=name)

            for key in attr.keys():
                self.graph.graph[key] = attr[key]
        else:
            self.graph = graph

        self.last_element_id = 100000
        self.last_joint_id = 200000

    def write(self, path):
        print(f"Writing to '{path}'...", end='')
        try:
            with open(path, 'w') as file:
                file.write(json.dumps(nx.node_link_data(self.graph), indent=4, sort_keys=True))
                print("OK")
        except Exception as e:
            print(f"FAILED ({e})")

    @staticmethod
    def read(path):
        print(f"Reading '{path}'...", end='')
        try:
            with open(path, 'r') as file:
                s = Structure(nx.node_link_graph(json.loads(file.read())))
                print("OK")
                return s
        except Exception as e:
            print(f"FAILED ({e})")

    def __getitem__(self, key):
        return self.graph.graph[key]

    def __eq__(self, other):
        return nx.utils.misc.graphs_equal(self.graph, other.graph)

    def __str__(self):
        return f"Structure({self.graph.name}) with {len(self.nodes)} nodes and {len(self.edges)} edges."

    @property
    def nodes(self):
        return self.graph.nodes

    @property
    def edges(self):
        return self.graph.edges

    def keys(self):
        return self.graph.graph.keys()

    def values(self):
        return self.graph.graph.values()

    def increment_joint_id(self):
        self.last_joint_id += 1
        return self.last_joint_id
        return f"J{self.last_joint_id:04d}"
    
    def increment_element_id(self):
        self.last_element_id += 1
        return self.last_element_id
        return f"E{self.last_element_id:04d}"

    def add_element(self, id=None, data={}):
        if id is None:
            id = self.increment_element_id()
            while id in self.graph.nodes:
                id = self.increment_element_id()

        if id in self.graph.nodes:
            raise ValueError(f"Id '{id}' already exists!")
        data["node_type"] = NodeType.Element
        self.graph.add_nodes_from([(id, data)])
        return id

    def add_elements(self, ids, datas=[]):
        new_ids = []
        for (id, data) in zip(ids, datas):
            data["node_type"] = NodeType.Element
            if id is None:
                id = self.increment_element_id()
                while id in self.graph.nodes:
                    id = self.increment_element_id()
            if id in self.graph.nodes:
                raise ValueError(f"Id '{id}' already exists!")
            self.graph.add_nodes_from([(id, data)])
            new_ids.append(id)
        return new_ids

    def add_joint(self, id=None, data={}):
        if id is None:
            id = self.increment_element_id()
            while id in self.graph.nodes:
                id = self.increment_element_id()            
        if id in self.graph.nodes:
            raise ValueError(f"Id '{id}' already exists!")
        data["node_type"] = NodeType.Joint
        self.graph.add_nodes_from([(id, data)])

    def join(self, element_ids=[], id=None, data={}):
        if id is None:
            id = self.increment_joint_id()
            while id in self.graph.nodes:
                id = self.increment_joint_id()
        data["joint_valence"] = len(element_ids)
        self.add_joint(id, data)
        return id

        for element_id in element_ids:
            self.graph.add_edge(element_id, id)

