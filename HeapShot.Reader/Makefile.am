
CSC = gmcs -debug
CSFLAGS = -target:library

TARGET = HeapShot.Reader.dll

CSFILES =  \
	FieldInfo.cs \
	ObjectInfo.cs \
	ObjectMapFileReader.cs \
	ObjectReference.cs \
	ReferenceNode.cs \
	TypeInfo.cs
	

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
