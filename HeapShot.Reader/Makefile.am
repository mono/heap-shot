
CSC = gmcs -debug
CSFLAGS = -target:library

TARGET = HeapShot.Reader.dll

CSFILES =  \
	Buffer.cs \
	Event.cs \
	EventVisitor.cs \
	FieldInfo.cs \
	Header.cs \
	LogBuffer.cs \
	ObjectInfo.cs \
	ObjectMapFileReader.cs \
	ObjectReference.cs \
	ReferenceNode.cs \
	TypeInfo.cs \
	Util.cs
	

REFS = -r:System
SRCDIR_CSFILES = $(CSFILES:%=$(srcdir)/%)

$(TARGET): $(SRCDIR_CSFILES)
	$(CSC) -out:$@ $(CSFLAGS) $(REFS) $^

all: $(TARGET)

install-data-local: $(TARGET)
	$(mkinstalldirs) $(DESTDIR)$(pkglibdir)
	$(INSTALL_DATA) $(TARGET) $(DESTDIR)$(pkglibdir)

uninstall-local:
	cd $(DESTDIR)$(pkglibdir) && rm -f $(TARGET)

EXTRA_DIST = 			\
	$(CSFILES)

CLEANFILES =			\
	$(TARGET)		\
	$(TARGET).mdb
