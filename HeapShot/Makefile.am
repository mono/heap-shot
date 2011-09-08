
CSC = dmcs -debug
CSFLAGS = -target:exe

TARGET = HeapShot.exe
WRAPPER = heap-shot

CSFILES =  \
	HeapShot.cs \
	ReferenceTreeReport.cs


RES = 
bin_SCRIPTS =			\
	$(WRAPPER)

SRCDIR_CSFILES = $(CSFILES:%=$(srcdir)/%)

REFS =  \
	-r:../HeapShot.Reader/HeapShot.Reader.dll \
	-r:System

$(TARGET): $(SRCDIR_CSFILES)
	$(CSC) -out:$@ $(CSFLAGS) $(REFS) $^

all: $(TARGET) $(WRAPPER)

install-data-local: $(TARGET)
	$(mkinstalldirs) $(DESTDIR)$(pkglibdir)
	$(INSTALL_DATA) $(TARGET) $(DESTDIR)$(pkglibdir)

uninstall-local:
	cd $(DESTDIR)$(pkglibdir) && rm -f $(TARGET)


EXTRA_DIST = 			\
	$(CSFILES)		\
	$(WRAPPER).in

CLEANFILES =			\
	$(TARGET)		\
	$(TARGET).mdb		\
	$(WRAPPER)
